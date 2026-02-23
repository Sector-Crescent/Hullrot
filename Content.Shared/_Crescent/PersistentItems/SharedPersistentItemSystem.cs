using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Toolshed.Commands.Values;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Content.Shared._Crescent.PersistentItems;

/// <summary>
/// :)
/// </summary>
public abstract class SharedPersistentItemStorageSystem : EntitySystem
{
    [Dependency] private readonly IDependencyCollection _dependencyCollection = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;

    // the robusttoolbox sandbox makes me want to commit suicide.
    public static Stream GenerateStreamFromString(string s)
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(s);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    public bool? ReadFromString(string data, out MappingDataNode? node)
    {
        node = null;
        using var reader = new StreamReader(GenerateStreamFromString(data));
        var documents = DataNodeParser.ParseYamlStream(reader).ToArray();
        node = (MappingDataNode) documents[0].Root;
        return true;
    }

    public string WriteToString(MappingDataNode data)
    {
        var document = new YamlDocument(data.ToYaml());
        var yamlstream = new YamlStream { document };

        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        yamlstream.Save(writer, false);
        writer.Flush();
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public MappingDataNode? GetItemData(EntityUid uid, SerializationOptions? options = null)
    {
        var opts = options ?? SerializationOptions.Default;

        MappingDataNode data;

        try
        {
            (data, _) = _mapLoader.SerializeEntitiesRecursive([uid], opts);
        }
        catch (Exception e)
        {
            Log.Error($"Caught exception while trying to serialize entities:\n{e}");
            return null;
        }
        return data;
    }

    public bool LoadItem(MappingDataNode data, [NotNullWhen(true)] out EntityUid? uid)
    {
        uid = null;
        var deserializer = new EntityDeserializer(_dependencyCollection, data, DeserializationOptions.Default);

        if (!deserializer.TryProcessData())
        {
            return false;
        }
        try
        {
            deserializer.CreateEntities();
        }
        catch (Exception e)
        {
            Log.Error($"Caught exception while creating entities for loaded entity: {e}");
            _mapLoader.Delete(deserializer.Result);
            return false;
        }
        uid = deserializer.Result.Entities.FirstOrDefault();
        return true;
    }

    public EntityUid AtemptClone(EntityUid uid)
    {
        var data = GetItemData(uid);
        if (data == null)
            return EntityUid.Invalid;

        if (!LoadItem(data, out var newUid))
            return EntityUid.Invalid;

        return newUid.Value;
    }

    public EntityUid AtemptCloneString(EntityUid uid)
    {
        var data = GetItemData(uid);
        if (data == null)
            return EntityUid.Invalid;

        var datastr = WriteToString(data);

        var readstr = ReadFromString(datastr, out var newdata);
        if (readstr != true || newdata == null)
            return EntityUid.Invalid;

        if (!LoadItem(newdata, out var newUid))
            return EntityUid.Invalid;

        return newUid.Value;
    }
}
