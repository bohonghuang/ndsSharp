using System.Diagnostics;
using System.Reflection;
using ndsSharp.Core.Data;
using ndsSharp.Core.Extensions;
using ndsSharp.Core.Objects;
using ndsSharp.Core.Objects.Exports;
using ndsSharp.Core.Objects.Exports.Archive;
using ndsSharp.Core.Objects.Exports.Sounds;
using ndsSharp.Core.Objects.Files;
using ndsSharp.Core.Objects.Rom;
using ndsSharp.Core.Plugins;
using Serilog;

namespace ndsSharp.Core.Providers;

public class NdsFileProvider : IFileProvider
{
    public Dictionary<string, RomFile> Files { get; set; } = [];
    public Dictionary<Type, BasePlugin> Plugins { get; set; } = [];
    
    public RomHeader Header;
    public RomBanner Banner;

    public bool UnpackNARCFiles = false;
    public bool UnpackSDATFiles = false;

    private AllocationTable _allocationTable;
    private NameTable _nameTable;
    
    private BaseReader _reader;
    
    public NdsFileProvider(FileInfo romFile) : this(romFile.FullName)
    {
    }

    public NdsFileProvider(string filePath)
    {
        _reader = new BaseReader(File.ReadAllBytes(filePath));
    }

    public void Initialize()
    {
        Header = new RomHeader(_reader);

        _reader.Position = (int) Header.BannerOffset;
        Banner = new RomBanner(_reader);

        _allocationTable = new AllocationTable(_reader.LoadPointer(Header.FatPointer));
        _nameTable = new NameTable(_reader.LoadPointer(Header.FntPointer));
        
        Mount(_allocationTable, _nameTable);

        if (UnpackNARCFiles)
        {
            var narcFiles = GetAllFilesOfType<NARC>().ToArray();
            foreach (var narcFile in narcFiles)
            {
                if (!TryLoadObject<NARC>(narcFile, out var narc))
                {
                    Log.Warning("Failed to mount NARC {Path}", narcFile.Path);
                    continue;
                }
                
                var basePath = narcFile.Path.Replace(".narc", string.Empty);
                foreach (var (path, file) in narc.Files)
                {
                    var newPath = basePath + $"/{path}";
                    Files[newPath] = new RomFile(newPath, file.Pointer.GlobalFrom(narc.Image.Reader), narcFile);
                }
                
                Files.Remove(narcFile.Path);
            }
        }

        if (UnpackSDATFiles)
        {
            var sdatFiles = GetAllFilesOfType<SDAT>().ToArray();
            foreach (var sdatFile in sdatFiles)
            {
                if (!TryLoadObject<SDAT>(sdatFile, out var sdat))
                {
                    Log.Warning("Failed to mount SDAT {Path}", sdatFile.Path);
                    continue;
                }
                
                var basePath = sdatFile.Path.Replace(".sdat", string.Empty);
                foreach (var soundType in Enum.GetValues<SoundFileType>())
                {
                    if (soundType is SoundFileType.GroupPlayer or SoundFileType.StreamPlayer or SoundFileType.Group) continue;
                    
                    var typeName = soundType.GetDescription();
                    var symbols = sdat.Symbols.Records[soundType];
                    var infos = sdat.Info.Records[soundType];

                    for (ushort index = 0; index < symbols.Count; index++)
                    {
                        var info = infos[index];
                        var data = sdat.FileAllocationTable.Pointers[info.FileID];
                        
                        var newPath = basePath + $"/{typeName}/{symbols[index]}.{typeName}".ToLower();
                        Files[newPath] = new SDATRomFile(newPath, data.GlobalFrom(sdat.Reader), info, index, sdatFile);
                    }
                }
                
                Files.Remove(sdatFile.Path);
            }
        }
        
        LoadPlugins();
    }

    protected void Mount(AllocationTable allocationTable, NameTable nameTable)
    {
        for (ushort id = 0; id < allocationTable.Pointers.Count; id++)
        {
            var pointer = allocationTable.Pointers[id];
            if (pointer.Length <= 0) continue;
            
            if (id >= nameTable.FirstId)
            {
                var fileName = nameTable.FilesById[id];
                if (!fileName.Contains('.')) // detect extension
                {
                    var extension = _reader.PeekString(4, pointer.Offset).TrimEnd('0').ToLower();
                    if (FileTypeRegistry.Contains(extension))
                    {
                        fileName += $".{extension}";
                    }
                    else
                    {
                        fileName += ".bin";
                    }
                }

                Files[fileName] = new RomFile(fileName, pointer);
            }
            else
            {
                var fileName = $"overlays/{id}.bin";
                Files[fileName] = new RomFile(fileName, pointer);
            }
        }
    }

    protected void LoadPlugins()
    {
        try
        {
            var pluginsAssembly = Assembly.Load("ndsSharp.Plugins");
            var pluginTypes = pluginsAssembly.DefinedTypes.Where(type => type.IsAssignableTo(typeof(BasePlugin)));
            foreach (var pluginType in pluginTypes)
            {
                var pluginInstance = Activator.CreateInstance(pluginType) as BasePlugin;
                if (pluginInstance is null) continue;
                if (!pluginInstance.GameCodes.Contains(Header.GameCode)) continue;

                pluginInstance.Owner = this;
                pluginInstance.OnLoaded();
            
                Plugins[pluginType] = pluginInstance;
            }
        }
        catch (Exception e)
        {
            Log.Error(e.ToString());
        }
    }

    public T GetPlugin<T>() where T : BasePlugin
    {
        return (T) Plugins[typeof(T)];
    }
    
    public IEnumerable<RomFile> GetAllFilesOfType<T>() where T : NdsObject, new()
    {
        var accessor = new T();
        return Files.Values.Where(file => file.Type.Equals(accessor.Magic, StringComparison.OrdinalIgnoreCase));
    }
    
    public T LoadObject<T>(string path) where T : BaseDeserializable, new() => LoadObject<T>(Files[path]);
    
    public T LoadObject<T>(RomFile file) where T : BaseDeserializable, new() => CreateReader(file).ReadObject<T>(dataModifier: obj => obj.Owner = file);
    
    public bool TryLoadObject<T>(string path, out T data) where T : BaseDeserializable, new() => TryLoadObject(Files[path], out data);
    
    public bool TryLoadObject<T>(RomFile file, out T data) where T : BaseDeserializable, new()
    {
        data = null!;
        try
        {
            data = LoadObject<T>(file);
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e.ToString());
            return false;
        }
    }

    public BaseReader CreateReader(RomFile file)
    {
        return _reader.LoadPointer(file.Pointer);
    }
    
    public BaseReader CreateReader(string path) => CreateReader(Files[path]);

    public void LogFileStats()
    {
        var fileBreakdown = new Dictionary<string, int>();
        foreach (var (path, file) in Files)
        {
            fileBreakdown.TryAdd(file.Type, 0);
            fileBreakdown[file.Type]++;
        }

        fileBreakdown = fileBreakdown.OrderByDescending(x => x.Value).ToDictionary();

        Log.Information("Total Files: {Count}", Files.Count);
        foreach (var (type, count) in fileBreakdown)
        {
            Log.Information("{Type}: {Count}", type, count);
        }
    }
}