using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Robust.Shared.Modules;

internal record RobustAsmStream : IDisposable
{
    internal string AssemblyName { get; private set; }

    internal string? Path { get; private set; }

    private Stream _validAsmStream = default!;
    //This is sanityChecked to make sure that it actually is an assembly stream and not some other random garbage
    internal Stream AssemblyStream
    {
        get
        {
            if (!IsValid)
            {
                throw new Exception($"{this} stream is not valid!");
            }
            return _validAsmStream;
        }
        private set => _validAsmStream = value;
    }

    private Stream? _validPdbStream;
    //This is sanityChecked to make sure that it actually is an assembly stream and not some other random garbage
    internal Stream? PdbStream
    {
        get => _validPdbStream;
        private set => _validPdbStream = value;
    }

    internal bool IsValid { get; private set; } = true;
    internal bool HasPdb => _validPdbStream != null;

    public RobustAsmStream(Stream AssemblyStream, Stream? PdbStream, bool copyStreams = false, string? path = null, bool throwIfInvalid = false)
    {
        Stream asmSteam = AssemblyStream;
        Stream? pdbStream = PdbStream;
        Path = path;
        if (copyStreams)
        {
            AssemblyStream.Position = 0;
            asmSteam = new MemoryStream();
            AssemblyStream.CopyTo(asmSteam);
            AssemblyStream.Position = 0;
            if (PdbStream != null)
            {
                PdbStream.Position = 0;
                pdbStream = new MemoryStream();
                PdbStream.CopyTo(pdbStream);
                PdbStream.Position = 0;
            }
        }

        AssemblyName = CheckAssemblySteam(asmSteam, throwIfInvalid);
        _validAsmStream.Position = 0;

        if (pdbStream == null)
            return;
        pdbStream.Position = 0;
        _validPdbStream = pdbStream;
    }

    public void Dispose()
    {
        _validAsmStream.Dispose();
        _validPdbStream?.Dispose();
    }

    private string CheckAssemblySteam(Stream asmStream, bool throwIfInvalid)
    {
        try
        {
            using var reader = MakePEReader(asmStream, true);
            MetadataReader metaReader = reader.GetMetadataReader();
            return metaReader.GetString(metaReader.GetAssemblyDefinition().Name);
        }
        catch (BadImageFormatException e)
        {
            if (throwIfInvalid)
                throw;

            IsValid = false;
            return string.Empty;
        }
    }
    internal static PEReader MakePEReader(Stream stream, bool leaveOpen=false, PEStreamOptions options=PEStreamOptions.Default)
    {
        if (!stream.CanSeek)
            stream = leaveOpen ? stream.CopyToMemoryStream() : stream.ConsumeToMemoryStream();

        if (leaveOpen)
            options |= PEStreamOptions.LeaveOpen;

        return new PEReader(stream, options);
    }
}
