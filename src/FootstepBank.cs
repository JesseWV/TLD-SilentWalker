using System.Text;

namespace SilentWalker;

internal static class FootstepBank
{

    internal const int BuilderVersion = 3;

    internal const string BankName = "SilentWalker_Footsteps";
    private const uint BankVersion = 150;
    internal const string EventFootsteps = "SW_Play_Footsteps";
    internal const string EventLimp = "SW_Play_FootstepsLimp";
    internal const string ParamFootstepVolume = "SW_FootstepVolume";
    internal const string ParamGroundVolume = "SW_GroundVolume";
    internal const string ParamClothingVolume = "SW_ClothingVolume";
    internal static readonly string[] PackVolumeParams = { "SW_PackGeneralVolume", "SW_PackWoodVolume", "SW_PackMetalVolume", "SW_PackWaterVolume" };
    internal const string ParamPackMuffle = "SW_PackMuffle";
    internal const string ParamMetalWrap = "SW_MetalWrap";
    internal static readonly string[] WeightParams = { "SW_WeightGeneral", "SW_WeightMetal", "SW_WeightWood", "SW_WeightWater" };
    private static readonly string[] GameWeightParams = { "InventoryWeightGeneral", "InventoryWeightMetal", "InventoryWeightWood", "InventoryWeightWater" };

    private const uint Ground = 526378730, Sweetener = 539323191, LimpScuff = 28496718;
    private const uint Clothing = 903382265, Pack = 645901910, Metal = 936748052;
    private const uint General = 294804098, Wood = 324347736, Water = 940422957;
    private const uint FootMixer = 922544432, ClothingMixer = 486574523, TopMixer = 422767709;
    private static readonly uint[] Roots = { Ground, Sweetener, LimpScuff, Clothing, Pack };
    private static readonly uint[] Ancestors = { FootMixer, ClothingMixer, TopMixer };

    private const byte ParamVolume = 0, ParamLowPass = 2;
    private const byte AccumAdditive = 2, ScalingNone = 0, ScalingDb = 2;
    private const uint InterpLinear = 4;
    private const float MuffleMaxLowPass = 60f, MetalWrapMaxLowPass = 40f;
    private const string Prefix = "sw_fs_";

    internal static uint Fnv(string name)
    {
        uint h = 2166136261;
        foreach (byte b in Encoding.ASCII.GetBytes(name.ToLowerInvariant()))
        {
            h = unchecked(h * 16777619) ^ b;
        }

        return h;
    }

    private static readonly byte[] BankHeader = Convert.FromHexString(
        "424B48442C000000" + "96000000d758570b3e5d7017100000000000000000000000f7369f9d77fd94f3cb7f4c6d46462e8300000000");

    internal static byte[] Build(byte[] gameBank)
    {
        var chunks = ReadChunks(gameBank);
        if (!chunks.TryGetValue("BKHD", out var bkhd) || !chunks.TryGetValue("HIRC", out var hirc))
        {
            throw new InvalidDataException("not a Wwise bank (no BKHD or HIRC)");
        }

        if (BitConverter.ToUInt32(gameBank, bkhd.Offset) != BankVersion)
        {
            throw new InvalidDataException($"game bank version {BitConverter.ToUInt32(gameBank, bkhd.Offset)}, expected {BankVersion}");
        }

        var objects = IndexHirc(gameBank, hirc.Offset, hirc.Length);

        var wanted = new HashSet<uint>();
        var parsed = new Dictionary<uint, HircObject>();
        var stack = new Stack<uint>(Roots);
        while (stack.Count > 0)
        {
            uint id = stack.Pop();
            if (!wanted.Add(id))
            {
                continue;
            }

            var obj = Parse(gameBank, objects, id);
            parsed[id] = obj;
            foreach (var child in obj.Children)
            {
                stack.Push(child.Value);
            }
        }

        foreach (uint id in Ancestors)
        {
            wanted.Add(id);
            parsed[id] = Parse(gameBank, objects, id);
        }

        var newId = new Dictionary<uint, uint>();
        foreach (uint id in wanted)
        {
            newId[id] = Fnv(Prefix + id);
        }

        var rebind = new Dictionary<uint, uint>();
        for (int i = 0; i < GameWeightParams.Length; i++)
        {
            rebind[Fnv(GameWeightParams[i])] = Fnv(WeightParams[i]);
        }

        var added = new Dictionary<uint, string[]>
        {
            [TopMixer] = new[] { ParamFootstepVolume },
            [FootMixer] = new[] { ParamFootstepVolume },
            [Ground] = new[] { ParamGroundVolume },
            [LimpScuff] = new[] { ParamGroundVolume },
            [Clothing] = new[] { ParamClothingVolume },
            [Pack] = new[] { ParamPackMuffle },
            [Metal] = new[] { ParamMetalWrap, PackVolumeParams[2] },
            [General] = new[] { PackVolumeParams[0] },
            [Wood] = new[] { PackVolumeParams[1] },
            [Water] = new[] { PackVolumeParams[3] },
        };

        var copies = new List<(int Start, byte[] Data)>();
        foreach (uint id in wanted)
        {
            var obj = parsed[id];
            var data = new List<byte>(new ArraySegment<byte>(gameBank, obj.Start, obj.Length));
            foreach (var field in obj.Ids)
            {
                uint v = BitConverter.ToUInt32(gameBank, obj.Start + field.Offset);
                uint? replacement = null;
                if (field.Kind == IdKind.Curve)
                {

                    replacement = Fnv($"{Prefix}rtpcCurveID_{v}");
                }
                else if (field.Kind == IdKind.Layer)
                {
                    replacement = Fnv($"{Prefix}ulLayerID_{v}");
                }
                else if (newId.TryGetValue(v, out uint n))
                {
                    replacement = n;
                }
                else if (rebind.TryGetValue(v, out uint r))
                {
                    replacement = r;
                }

                if (replacement.HasValue)
                {
                    WriteU32(data, field.Offset, replacement.Value);
                }
            }

            var edits = new List<(int Offset, byte[]? Insert, int CountAt)>();
            if (Array.IndexOf(Ancestors, id) >= 0)
            {
                int kept = 0;
                foreach (var child in obj.Children)
                {
                    if (wanted.Contains(child.Value))
                    {
                        kept++;
                    }
                    else
                    {
                        edits.Add((child.Offset, null, -1));
                    }
                }

                WriteU32(data, obj.NumChildsOffset, (uint)kept);
            }

            if (added.TryGetValue(id, out var names))
            {
                foreach (string name in names)
                {
                    edits.Add((obj.NumCurvesOffset + 2, RtpcEntry(name, id), obj.NumCurvesOffset));
                }
            }

            foreach (var (offset, insert, countAt) in edits.OrderByDescending(e => e.Offset).ToList())
            {
                if (insert == null)
                {
                    data.RemoveRange(offset, 4);
                }
                else
                {
                    ushort count = (ushort)(data[countAt] | data[countAt + 1] << 8);
                    count++;
                    data[countAt] = (byte)count;
                    data[countAt + 1] = (byte)(count >> 8);
                    data.InsertRange(offset, insert);
                }
            }

            WriteU32(data, 1, (uint)(data.Count - 5));
            copies.Add((obj.Start, data.ToArray()));
        }

        copies.Sort((a, b) => a.Start.CompareTo(b.Start));

        uint bankId = Fnv(BankName);
        var items = new List<byte[]>();
        foreach (var c in copies)
        {
            items.Add(c.Data);
        }

        AddEvent(items, EventFootsteps, new[] { Ground, Clothing, Pack, Sweetener }, newId, bankId);
        AddEvent(items, EventLimp, new[] { Ground, LimpScuff }, newId, bankId);

        var output = new List<byte>();
        var header = (byte[])BankHeader.Clone();
        BitConverter.GetBytes(bankId).CopyTo(header, 12);
        output.AddRange(header);

        int hircLength = 4;
        foreach (var item in items)
        {
            hircLength += item.Length;
        }

        output.AddRange(Encoding.ASCII.GetBytes("HIRC"));
        output.AddRange(BitConverter.GetBytes(hircLength));
        output.AddRange(BitConverter.GetBytes(items.Count));
        foreach (var item in items)
        {
            output.AddRange(item);
        }

        return output.ToArray();
    }

    internal static void VerifyEvents(byte[] eventsBank)
    {
        var chunks = ReadChunks(eventsBank);
        if (!chunks.TryGetValue("HIRC", out var hirc))
        {
            throw new InvalidDataException("events bank has no HIRC");
        }

        var index = IndexHirc(eventsBank, hirc.Offset, hirc.Length);
        CheckEvent(eventsBank, index, 3854155799, "Play_Footsteps", new[] { Ground, Clothing, Pack, Sweetener });
        CheckEvent(eventsBank, index, 2480385009, "Play_FootstepsLimp", new[] { Ground, LimpScuff });
    }

    private static void CheckEvent(byte[] bank, Dictionary<uint, (int Start, byte Type)> index, uint eventId, string name, uint[] expected)
    {
        if (!index.TryGetValue(eventId, out var ev) || ev.Type != 4)
        {
            throw new InvalidDataException($"event {name} is missing from the game's events bank");
        }

        var r = new Reader(bank, ev.Start + 9);
        uint count = r.Var();
        var targets = new List<uint>();
        for (int i = 0; i < count; i++)
        {
            uint actionId = r.U32();
            if (!index.TryGetValue(actionId, out var action) || action.Type != 3)
            {
                throw new InvalidDataException($"event {name}: action {actionId} is missing");
            }

            ushort actionType = BitConverter.ToUInt16(bank, action.Start + 9);
            if (actionType != 0x0403)
            {
                throw new InvalidDataException($"event {name}: action {actionId} is type 0x{actionType:X4}, not Play");
            }

            targets.Add(BitConverter.ToUInt32(bank, action.Start + 11));
        }

        if (targets.Count != expected.Length || !targets.OrderBy(t => t).SequenceEqual(expected.OrderBy(t => t)))
        {
            throw new InvalidDataException($"event {name} now plays [{string.Join(", ", targets)}], the copy reproduces [{string.Join(", ", expected)}]");
        }
    }

    private static void AddEvent(List<byte[]> items, string eventName, uint[] targets, Dictionary<uint, uint> newId, uint bankId)
    {
        var actionIds = new List<uint>();
        foreach (uint target in targets)
        {
            uint actionId = Fnv($"sw_action_{Prefix}{eventName}_{target}");
            var body = new List<byte>();
            body.AddRange(BitConverter.GetBytes(actionId));
            body.AddRange(BitConverter.GetBytes((ushort)0x0403));
            body.AddRange(BitConverter.GetBytes(newId[target]));
            body.AddRange(new byte[] { 0, 0, 0, 4 });
            body.AddRange(BitConverter.GetBytes(bankId));
            body.AddRange(BitConverter.GetBytes(0u));
            items.Add(Section(3, body));
            actionIds.Add(actionId);
        }

        var ev = new List<byte>();
        ev.AddRange(BitConverter.GetBytes(Fnv(eventName)));
        ev.Add((byte)actionIds.Count);
        foreach (uint a in actionIds)
        {
            ev.AddRange(BitConverter.GetBytes(a));
        }

        items.Add(Section(4, ev));
    }

    private static byte[] Section(byte type, List<byte> body)
    {
        var s = new List<byte> { type };
        s.AddRange(BitConverter.GetBytes(body.Count));
        s.AddRange(body);
        return s.ToArray();
    }

    private static byte[] RtpcEntry(string name, uint node)
    {
        bool lowPass = name == ParamPackMuffle || name == ParamMetalWrap;
        uint curveId = lowPass ? Fnv("sw_curve_" + (name == ParamPackMuffle ? "packmuffle" : "metalwrap")) : Fnv($"sw_curve_{name.ToLowerInvariant()}_{node}");
        float[] points = lowPass
            ? new[] { 0f, 0f, 100f, name == ParamPackMuffle ? MuffleMaxLowPass : MetalWrapMaxLowPass }
            : new[] { 0f, -1f, 100f, 0f };
        var b = new List<byte>();
        b.AddRange(BitConverter.GetBytes(Fnv(name)));
        b.Add(0);
        b.Add(AccumAdditive);
        b.Add(lowPass ? ParamLowPass : ParamVolume);
        b.AddRange(BitConverter.GetBytes(curveId));
        b.Add(lowPass ? ScalingNone : ScalingDb);
        b.AddRange(BitConverter.GetBytes((ushort)2));
        for (int i = 0; i < 4; i += 2)
        {
            b.AddRange(BitConverter.GetBytes(points[i]));
            b.AddRange(BitConverter.GetBytes(points[i + 1]));
            b.AddRange(BitConverter.GetBytes(InterpLinear));
        }

        return b.ToArray();
    }

    private static void WriteU32(List<byte> data, int offset, uint value)
    {
        data[offset] = (byte)value;
        data[offset + 1] = (byte)(value >> 8);
        data[offset + 2] = (byte)(value >> 16);
        data[offset + 3] = (byte)(value >> 24);
    }

    internal readonly record struct Chunk(int Offset, int Length);

    internal static Dictionary<string, Chunk> ReadChunks(byte[] bank)
    {
        var chunks = new Dictionary<string, Chunk>();
        int pos = 0;
        while (pos + 8 <= bank.Length)
        {
            string tag = Encoding.ASCII.GetString(bank, pos, 4);
            int length = BitConverter.ToInt32(bank, pos + 4);
            if (length < 0 || pos + 8 + length > bank.Length)
            {
                throw new InvalidDataException($"chunk {tag} at {pos} overruns the file");
            }

            chunks[tag] = new Chunk(pos + 8, length);
            pos += 8 + length;
        }

        return chunks;
    }

    internal static Dictionary<uint, (int Start, byte Type)> IndexHirc(byte[] bank, int offset, int length)
    {
        var index = new Dictionary<uint, (int, byte)>();
        int count = BitConverter.ToInt32(bank, offset);
        int pos = offset + 4;
        for (int i = 0; i < count; i++)
        {
            if (pos + 9 > offset + length)
            {
                throw new InvalidDataException($"HIRC object {i} of {count} runs past the chunk");
            }

            byte type = bank[pos];
            int size = BitConverter.ToInt32(bank, pos + 1);
            if (size < 4 || pos + 5 + size > offset + length)
            {
                throw new InvalidDataException($"HIRC object {i} at {pos} declares {size} bytes, past the chunk");
            }

            uint id = BitConverter.ToUInt32(bank, pos + 5);
            if (!index.ContainsKey(id))
            {
                index[id] = (pos, type);
            }

            pos += 5 + size;
        }

        if (pos != offset + length)
        {
            throw new InvalidDataException("HIRC object sizes do not add up to the chunk");
        }

        return index;
    }

    internal enum IdKind { Plain, Curve, Layer }

    internal readonly record struct IdField(int Offset, string Name, IdKind Kind);

    internal sealed class HircObject
    {
        internal int Start;
        internal int Length;
        internal byte Type;
        internal readonly List<IdField> Ids = new();
        internal readonly List<(int Offset, uint Value)> Children = new();
        internal int NumChildsOffset = -1;
        internal int NumCurvesOffset = -1;
    }

    internal static HircObject Parse(byte[] bank, Dictionary<uint, (int Start, byte Type)> index, uint id)
    {
        if (!index.TryGetValue(id, out var at))
        {
            throw new InvalidDataException($"object {id} is missing from the game bank");
        }

        return Parse(bank, at.Start);
    }

    internal static HircObject Parse(byte[] bank, int start)
    {
        var r = new Reader(bank, start);
        var obj = new HircObject { Start = start, Type = r.U8() };
        int size = (int)r.U32();
        obj.Length = 5 + size;
        r.Id(obj, "ulID");
        switch (obj.Type)
        {
            case 2:
                uint plugin = r.U32();
                r.U8();
                r.Id(obj, "sourceID");
                r.U32();
                r.U8();
                if ((plugin & 0xF) == 2)
                {

                    uint paramSize = r.U32();
                    r.Skip((int)paramSize);
                }

                NodeBaseParams(r, obj);
                break;
            case 5:
                NodeBaseParams(r, obj);
                r.Skip(2 + 2 + 2 + 4 + 4 + 4 + 2 + 1 + 1 + 1 + 1);
                Children(r, obj);
                int items = r.U16();
                for (int i = 0; i < items; i++)
                {
                    r.Id(obj, "ulPlayID");
                    r.U32();
                }

                break;
            case 6:
                NodeBaseParams(r, obj);
                r.U8();
                r.Id(obj, "ulGroupID");
                r.Id(obj, "ulDefaultSwitch");
                r.U8();
                Children(r, obj);
                uint groups = r.U32();
                for (int i = 0; i < groups; i++)
                {
                    r.Id(obj, "ulSwitchID");
                    uint n = r.U32();
                    for (int j = 0; j < n; j++)
                    {
                        r.Id(obj, "NodeID");
                    }
                }

                uint switchParams = r.U32();
                for (int i = 0; i < switchParams; i++)
                {
                    r.Id(obj, "ulNodeID");
                    r.Skip(1 + 1 + 4 + 4);
                }

                break;
            case 7:
                NodeBaseParams(r, obj);
                Children(r, obj);
                break;
            case 9:
                NodeBaseParams(r, obj);
                Children(r, obj);
                uint layers = r.U32();
                for (int i = 0; i < layers; i++)
                {
                    r.Id(obj, "ulLayerID", IdKind.Layer);
                    InitialRtpc(r, obj, record: false);
                    r.Id(obj, "rtpcID");
                    r.U8();
                    uint assocs = r.U32();
                    for (int j = 0; j < assocs; j++)
                    {
                        r.Id(obj, "ulAssociatedChildID");
                        uint points = r.U32();
                        r.Skip((int)points * 12);
                    }
                }

                r.U8();
                break;
            default:
                throw new InvalidDataException($"object at {start} has unsupported HIRC type {obj.Type}");
        }

        if (r.Pos != start + obj.Length)
        {
            throw new InvalidDataException($"object at {start} (type {obj.Type}) parsed {r.Pos - start} of {obj.Length} bytes");
        }

        return obj;
    }

    private static void Children(Reader r, HircObject obj)
    {
        obj.NumChildsOffset = r.Pos - obj.Start;
        uint n = r.U32();
        for (int i = 0; i < n; i++)
        {
            int offset = r.Pos - obj.Start;
            obj.Children.Add((offset, BitConverter.ToUInt32(r.Bank, r.Pos)));
            r.Id(obj, "ulChildID");
        }
    }

    private static void NodeBaseParams(Reader r, HircObject obj)
    {

        r.U8();
        int fx = r.U8();
        if (fx > 0)
        {
            r.U8();
            for (int i = 0; i < fx; i++)
            {
                r.U8();
                r.Id(obj, "fxID");
                r.U8();
            }
        }

        r.U8();
        int meta = r.U8();
        for (int i = 0; i < meta; i++)
        {
            r.U8();
            r.Id(obj, "fxID");
            r.U8();
        }

        r.Id(obj, "OverrideBusId");
        r.Id(obj, "DirectParentID");
        r.U8();

        int props = r.U8();
        r.Skip(props + props * 4);
        int ranged = r.U8();
        r.Skip(ranged + ranged * 8);

        int bits = r.U8();
        bool overrideParent = (bits & 0x01) != 0;
        bool listenerRelative = (bits & 0x02) != 0;
        if (overrideParent || listenerRelative)
        {
            if (listenerRelative)
            {
                int bits3d = r.U8();
                int positionType = (bits >> 5) & 0x3;
                if (positionType != 0)
                {
                    r.U8();
                    r.U32();
                    uint vertices = r.U32();
                    r.Skip((int)vertices * 16);
                    uint playlist = r.U32();
                    r.Skip((int)playlist * 8);
                    r.Skip((int)playlist * 12);
                }

                _ = bits3d;
            }
        }

        int aux = r.U8();
        if ((aux & 0x08) != 0)
        {
            for (int i = 0; i < 4; i++)
            {
                r.Id(obj, "auxID");
            }
        }

        r.Id(obj, "reflectionsAuxBus");

        r.Skip(1 + 1 + 2 + 1 + 1);

        uint stateProps = r.Var();
        for (int i = 0; i < stateProps; i++)
        {
            r.Var();
            r.U8();
            r.U8();
        }

        uint stateGroups = r.Var();
        for (int i = 0; i < stateGroups; i++)
        {
            r.Id(obj, "ulStateGroupID");
            r.U8();
            uint states = r.Var();
            for (int j = 0; j < states; j++)
            {
                r.Id(obj, "ulStateID");
                int cProps = r.U16();
                r.Skip(cProps * 2 + cProps * 4);
            }
        }

        InitialRtpc(r, obj, record: true);
    }

    private static void InitialRtpc(Reader r, HircObject obj, bool record)
    {
        if (record)
        {
            obj.NumCurvesOffset = r.Pos - obj.Start;
        }

        int curves = r.U16();
        for (int i = 0; i < curves; i++)
        {
            r.Id(obj, "RTPCID");
            r.U8();
            r.U8();
            r.Var();
            r.Id(obj, "rtpcCurveID", IdKind.Curve);
            r.U8();
            int points = r.U16();
            r.Skip(points * 12);
        }
    }

    private sealed class Reader
    {
        internal readonly byte[] Bank;
        internal int Pos;

        internal Reader(byte[] bank, int pos)
        {
            Bank = bank;
            Pos = pos;
        }

        internal byte U8()
        {
            Need(1);
            return Bank[Pos++];
        }

        private void Need(int n)
        {
            if (Pos + n > Bank.Length)
            {
                throw new InvalidDataException($"read past the end of the bank at {Pos}");
            }
        }

        internal ushort U16()
        {
            Need(2);
            ushort v = BitConverter.ToUInt16(Bank, Pos);
            Pos += 2;
            return v;
        }

        internal uint U32()
        {
            Need(4);
            uint v = BitConverter.ToUInt32(Bank, Pos);
            Pos += 4;
            return v;
        }

        internal uint Var()
        {
            uint v = 0;
            byte b;
            do
            {
                b = U8();
                v = (v << 7) | (uint)(b & 0x7F);
            }
            while ((b & 0x80) != 0);
            return v;
        }

        internal void Skip(int n) => Pos += n;

        internal void Id(HircObject obj, string name, IdKind kind = IdKind.Plain)
        {
            obj.Ids.Add(new IdField(Pos - obj.Start, name, kind));
            Pos += 4;
        }
    }
}
