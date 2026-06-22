using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using STB;

namespace typegen
{
    public class ShaderResource
    {
        public int register = -1;
        public string name = "";
    }

    public enum InterpolationMode
    {
        Default,
        Linear,
        Noperspective,
        Nointerpolation,
        Centroid,
        Sample,
        LinearCentroid,
        LinearSample,
        NoperspectiveCentroid,
        NoperspectiveSample
    }

    public class BufferEntry { 
        public string type;
        public string name;
        public int arraySize = 1;
        public string constantSize = "";
        public int offset = -1;
        public string semantic = "";
        public InterpolationMode interpolation;
    }
    

    public class ConstantBuffer : ShaderResource
    {
        public List<BufferEntry> entries = new List<BufferEntry>();
    }

    public enum BufferKind
    {
        Unknown,
        Buffer,
        StructuredBuffer,
        AppendStructuredBuffer,
        ConsumeStructuredBuffer,
        ByteAddressBuffer,
        RWBuffer,
        RWStructuredBuffer,
        RWByteAddressBuffer
    }

    public class Buffer : ShaderResource
    {
        public string elementType = "";
        public BufferKind kind;
    }

    public class Sampler : ShaderResource
    {
        public bool comparison;
    }

    public enum TextureType { 
        Buffer,
        Texture1D,
        Texture1DArray,
        Texture2D,
        Texture2DArray,
        Texture3D,
        TextureCube,
        TextureCubeArray,
        Texture2DMS,
        Texture2DMSArray,
        RWTexture1D,
        RWTexture1DArray,
        RWTexture2D,
        RWTexture2DArray,
        RWTexture3D,
        RWTexture2DMSArray,
        RWBuffer
    }

    public class StructDef
    {
        public string name;
        public List<BufferEntry> members = new List<BufferEntry>();
    }

    public class FunctionParam
    {
        public string type;
        public string name;
        public string semantic;
        public bool isConst;
        public bool isIn;
        public bool isOut;
        public bool isInOut;
        public bool isUniform;
    }

    public class FunctionDef
    {
        public string returnType;
        public string name;
        public List<FunctionParam> parameters = new List<FunctionParam>();
        public string semantic;
        public List<string> attributes = new List<string>();
        public int numThreadsX = -1;
        public int numThreadsY = -1;
        public int numThreadsZ = -1;
    }

    public class Texture : ShaderResource
    {
        public TextureType type;
    }

    public class HLSLProcessor
    {
        public List<ConstantBuffer> cbuffers = new List<ConstantBuffer>();
        public List<Buffer> buffers = new List<Buffer>();
        public List<Sampler> samplers = new List<Sampler>();
        public List<Texture> textures = new List<Texture>();
        public List<StructDef> structs = new List<StructDef>();
        public List<FunctionDef> functions = new List<FunctionDef>();

        HashSet<string> processedIncludes = new HashSet<string>();

        List<string> pendingAttrs = new List<string>();
        int pendingNX = -1, pendingNY = -1, pendingNZ = -1;

        void ClearPending()
        {
            pendingAttrs.Clear();
            pendingNX = pendingNY = pendingNZ = -1;
        }

        void SkipTemplateArgs(Lexer lexer)
        {
            if (lexer.Peek() != '<')
                return;
            lexer.GetToken(); // consume '<'
            int depth = 1;
            while (depth > 0 && lexer.GetToken() != Token.EOF)
            {
                if (lexer.token == '<') depth++;
                else if (lexer.token == '>') depth--;
            }
        }

        string CaptureTemplateArgs(Lexer lexer)
        {
            if (lexer.Peek() != '<')
                return "";
            lexer.GetToken(); // consume '<'
            int depth = 1;
            StringBuilder sb = new StringBuilder();
            while (depth > 0 && lexer.GetToken() != Token.EOF)
            {
                if (lexer.token == '<') depth++;
                else if (lexer.token == '>') { depth--; continue; }
                if (depth > 0)
                {
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(lexer.TokenText);
                }
            }
            return sb.ToString();
        }

        void ParseRegister(Lexer lexer, ShaderResource res)
        {
            if (lexer.token != ':')
                return;
            lexer.GetToken(); // register
            if (lexer.token == Token.ID && lexer.TokenText == "register")
            {
                lexer.GetToken(); // (
                lexer.GetToken(); // Xn
                res.register = int.Parse(lexer.TokenText.Substring(1));
                lexer.GetToken(); // )
            }
        }

        bool EvalIfCond(Lexer lexer, HashSet<string> defines)
        {
            lexer.GetToken();
            bool negate = false;
            if (lexer.token == '!')
            {
                negate = true;
                lexer.GetToken();
            }
            if (lexer.token == Token.IntLit)
                return negate ? lexer.int_number == 0 : lexer.int_number != 0;
            bool result;
            if (lexer.token == Token.ID && lexer.TokenText == "defined")
            {
                if (lexer.Peek() == '(')
                {
                    lexer.GetToken(); lexer.GetToken();
                    result = defines.Contains(lexer.TokenText);
                    lexer.GetToken(); // )
                }
                else
                {
                    lexer.GetToken();
                    result = defines.Contains(lexer.TokenText);
                }
            }
            else if (lexer.token == Token.ID)
                result = defines.Contains(lexer.TokenText);
            else
                result = true;
            return negate ? !result : result;
        }

        /// <summary>
        /// Processes a segment of code looking for cbuffers, samplers, buffers, and textures
        /// </summary>
        public void Process(string code)
        {
            Process(code, ".", null);
        }

        /// <summary>
        /// Processes a segment of code looking for resources and function signatures.
        /// workingDir is used for resolving #include paths.
        /// defines is an optional set of preprocessor symbols.
        /// </summary>
        public void Process(string code, string workingDir, HashSet<string> defines = null)
        {
            Lexer lexer = new Lexer(code);
            lexer.parse_preprocesser = true;
            HashSet<string> activeDefines = defines != null ? new HashSet<string>(defines) : new HashSet<string>();
            Stack<bool> skipStack = new Stack<bool>();
            bool skipping = false;

            while (lexer.GetToken() != Token.EOF)
            {
                if (lexer.token == '#')
                {
                    lexer.GetToken();
                    if (lexer.token != Token.ID) continue;
                    string dir = lexer.TokenText;

                    if (dir == "define")
                    {
                        lexer.GetToken();
                        if (!skipping && lexer.token == Token.ID)
                            activeDefines.Add(lexer.TokenText);
                    }
                    else if (dir == "undef")
                    {
                        lexer.GetToken();
                        if (lexer.token == Token.ID)
                            activeDefines.Remove(lexer.TokenText);
                    }
                    else if (dir == "ifdef")
                    {
                        lexer.GetToken();
                        bool cond = lexer.token == Token.ID && activeDefines.Contains(lexer.TokenText);
                        skipStack.Push(skipping);
                        if (!skipping && !cond)
                            skipping = true;
                    }
                    else if (dir == "ifndef")
                    {
                        lexer.GetToken();
                        bool cond = !(lexer.token == Token.ID && activeDefines.Contains(lexer.TokenText));
                        skipStack.Push(skipping);
                        if (!skipping && !cond)
                            skipping = true;
                    }
                    else if (dir == "if")
                    {
                        bool cond = EvalIfCond(lexer, activeDefines);
                        skipStack.Push(skipping);
                        if (!skipping && !cond)
                            skipping = true;
                    }
                    else if (dir == "elif")
                    {
                        if (skipStack.Count > 0)
                        {
                            bool parentSkip = skipStack.Peek();
                            if (!parentSkip)
                            {
                                if (skipping)
                                {
                                    bool cond = EvalIfCond(lexer, activeDefines);
                                    skipping = !cond;
                                }
                                else
                                {
                                    skipping = true;
                                }
                            }
                        }
                    }
                    else if (dir == "else")
                    {
                        if (skipStack.Count > 0)
                        {
                            bool parentSkip = skipStack.Peek();
                            if (!parentSkip)
                                skipping = !skipping;
                        }
                    }
                    else if (dir == "endif")
                    {
                        if (skipStack.Count > 0)
                            skipping = skipStack.Pop();
                    }
                    else if (dir == "include" && !skipping)
                    {
                        lexer.GetToken();
                        string includeFile = null;
                        if (lexer.token == Token.DQString)
                            includeFile = lexer.string_value;
                        else if (lexer.token == '<')
                        {
                            StringBuilder sb = new StringBuilder();
                            lexer.GetToken();
                            while (lexer.token != '>' && lexer.token != Token.EOF)
                            {
                                sb.Append(lexer.TokenText);
                                lexer.GetToken();
                            }
                            includeFile = sb.ToString();
                        }
                        if (!string.IsNullOrEmpty(includeFile))
                        {
                            string fullPath = Path.Combine(workingDir, includeFile);
                            if (processedIncludes.Add(fullPath) && File.Exists(fullPath))
                                Process(File.ReadAllText(fullPath), workingDir, activeDefines);
                        }
                    }
                    continue;
                }

                if (skipping)
                    continue;

                if (lexer.token == '[')
                {
                    int start = lexer.where_firstchar;
                    int depth = 1;
                    while (depth > 0 && lexer.GetToken() != Token.EOF)
                    {
                        if (lexer.token == '[') depth++;
                        else if (lexer.token == ']') depth--;
                    }
                    string attrText = lexer.input_stream.Substring(start, lexer.where_lastchar - start);
                    pendingAttrs.Add(attrText);

                    int nOpen = attrText.IndexOf('(');
                    if (nOpen > 0 && attrText.Substring(1, nOpen - 1).Trim() == "numthreads")
                    {
                        int nClose = attrText.LastIndexOf(')');
                        string[] parts = attrText.Substring(nOpen + 1, nClose - nOpen - 1).Split(',');
                        if (parts.Length == 3)
                        {
                            int.TryParse(parts[0].Trim(), out pendingNX);
                            int.TryParse(parts[1].Trim(), out pendingNY);
                            int.TryParse(parts[2].Trim(), out pendingNZ);
                        }
                    }
                    continue;
                }

                if (lexer.token == Token.ID)
                {
                    string text = lexer.TokenText;

                    if (text == "cbuffer")
                    {
                        ClearPending();
                        lexer.GetToken();
                        ConstantBuffer buff = new ConstantBuffer();

                        if (lexer.token == Token.ID)
                        {
                            buff.name = lexer.TokenText;
                            lexer.GetToken();
                        }
                        ParseRegister(lexer, buff);

                        lexer.GetToken(); // {
                        while (lexer.token != '}')
                        {
                            lexer.GetToken();

                            if (lexer.token == '}' || lexer.token == Token.EOF)
                                break;

                            BufferEntry entry = new BufferEntry();
                            entry.type = lexer.TokenText;

                            lexer.GetToken();
                            entry.name = lexer.TokenText;
                            if (lexer.Peek() == '[')
                            {
                                lexer.GetToken(); //[
                                lexer.GetToken();
                                if (lexer.token == Token.IntLit)
                                    entry.arraySize = (int)lexer.int_number;
                                else if (lexer.token == Token.ID)
                                    entry.constantSize = lexer.TokenText;
                                lexer.GetToken(); // ]
                            }
                            if (lexer.Peek() == ';')
                                lexer.GetToken();

                            buff.entries.Add(entry);
                        }
                        cbuffers.Add(buff);
                    }
                    else if (text == "tbuffer")
                    {
                        ClearPending();
                        lexer.GetToken();
                        ConstantBuffer buff = new ConstantBuffer();

                        if (lexer.token == Token.ID)
                        {
                            buff.name = lexer.TokenText;
                            lexer.GetToken();
                        }
                        ParseRegister(lexer, buff);

                        lexer.GetToken(); // {
                        while (lexer.token != '}')
                        {
                            lexer.GetToken();

                            if (lexer.token == '}' || lexer.token == Token.EOF)
                                break;

                            BufferEntry entry = new BufferEntry();
                            entry.type = lexer.TokenText;

                            lexer.GetToken();
                            entry.name = lexer.TokenText;
                            if (lexer.Peek() == '[')
                            {
                                lexer.GetToken(); //[
                                lexer.GetToken();
                                if (lexer.token == Token.IntLit)
                                    entry.arraySize = (int)lexer.int_number;
                                else if (lexer.token == Token.ID)
                                    entry.constantSize = lexer.TokenText;
                                lexer.GetToken(); // ]
                            }
                            if (lexer.Peek() == ';')
                                lexer.GetToken();

                            buff.entries.Add(entry);
                        }
                        cbuffers.Add(buff);
                    }
                    else if (text.StartsWith("Texture"))
                    {
                        ClearPending();
                        Texture tex = new Texture();
                        tex.type = (TextureType)Enum.Parse(typeof(TextureType), text);
                        SkipTemplateArgs(lexer);
                        lexer.GetToken();
                        tex.name = lexer.TokenText;
                        lexer.GetToken();
                        ParseRegister(lexer, tex);
                        textures.Add(tex);
                    }
                    else if (text == "Buffer")
                    {
                        ClearPending();
                        Buffer buf = new Buffer();
                        buf.kind = BufferKind.Buffer;
                        buf.elementType = CaptureTemplateArgs(lexer);
                        lexer.GetToken();
                        buf.name = lexer.TokenText;
                        lexer.GetToken();
                        ParseRegister(lexer, buf);
                        buffers.Add(buf);
                    }
                    else if (text == "SamplerState" || text == "SamplerComparisonState")
                    {
                        ClearPending();
                        Sampler sampler = new Sampler();
                        sampler.comparison = text == "SamplerComparisonState";
                        lexer.GetToken();
                        sampler.name = lexer.TokenText;
                        lexer.GetToken();
                        ParseRegister(lexer, sampler);
                        samplers.Add(sampler);
                    }
                    else if (text.StartsWith("RW"))
                    {
                        ClearPending();
                        if (text.StartsWith("RWTexture"))
                        {
                            Texture tex = new Texture();
                            tex.type = (TextureType)Enum.Parse(typeof(TextureType), text);
                            SkipTemplateArgs(lexer);
                            lexer.GetToken();
                            tex.name = lexer.TokenText;
                            lexer.GetToken();
                            ParseRegister(lexer, tex);
                            textures.Add(tex);
                        }
                        else
                        {
                            Buffer buf = new Buffer();
                            if (text == "RWBuffer")
                                buf.kind = BufferKind.RWBuffer;
                            else if (text == "RWStructuredBuffer")
                                buf.kind = BufferKind.RWStructuredBuffer;
                            else if (text == "RWByteAddressBuffer")
                                buf.kind = BufferKind.RWByteAddressBuffer;
                            buf.elementType = CaptureTemplateArgs(lexer);
                            lexer.GetToken();
                            buf.name = lexer.TokenText;
                            lexer.GetToken();
                            ParseRegister(lexer, buf);
                            buffers.Add(buf);
                        }
                    }
                    else if (text == "StructuredBuffer" || text == "AppendStructuredBuffer" || text == "ConsumeStructuredBuffer" || text == "ByteAddressBuffer")
                    {
                        ClearPending();
                        Buffer buf = new Buffer();
                        if (text == "StructuredBuffer")
                            buf.kind = BufferKind.StructuredBuffer;
                        else if (text == "AppendStructuredBuffer")
                            buf.kind = BufferKind.AppendStructuredBuffer;
                        else if (text == "ConsumeStructuredBuffer")
                            buf.kind = BufferKind.ConsumeStructuredBuffer;
                        else if (text == "ByteAddressBuffer")
                            buf.kind = BufferKind.ByteAddressBuffer;
                        buf.elementType = CaptureTemplateArgs(lexer);
                        lexer.GetToken();
                        buf.name = lexer.TokenText;
                        lexer.GetToken();
                        ParseRegister(lexer, buf);
                        buffers.Add(buf);
                    }
                    else if (text == "struct")
                    {
                        ClearPending();
                        StructDef def = new StructDef();
                        lexer.GetToken();
                        def.name = lexer.TokenText;
                        lexer.GetToken(); // {
                        lexer.GetToken();
                        while (lexer.token != '}')
                        {
                            if (lexer.token == Token.EOF)
                                break;

                            BufferEntry member = new BufferEntry();
                            InterpolationMode interpType = InterpolationMode.Default;
                            InterpolationMode interpMod = InterpolationMode.Default;
                            for (;;)
                            {
                                string t = lexer.TokenText;
                                if (t == "linear") { interpType = InterpolationMode.Linear; lexer.GetToken(); }
                                else if (t == "noperspective") { interpType = InterpolationMode.Noperspective; lexer.GetToken(); }
                                else if (t == "nointerpolation") { interpType = InterpolationMode.Nointerpolation; lexer.GetToken(); }
                                else if (t == "centroid") { interpMod = InterpolationMode.Centroid; lexer.GetToken(); }
                                else if (t == "sample") { interpMod = InterpolationMode.Sample; lexer.GetToken(); }
                                else break;
                            }
                            if (interpType == InterpolationMode.Default)
                                member.interpolation = interpMod;
                            else if (interpMod == InterpolationMode.Default)
                                member.interpolation = interpType;
                            else if (interpType == InterpolationMode.Linear && interpMod == InterpolationMode.Centroid)
                                member.interpolation = InterpolationMode.LinearCentroid;
                            else if (interpType == InterpolationMode.Linear && interpMod == InterpolationMode.Sample)
                                member.interpolation = InterpolationMode.LinearSample;
                            else if (interpType == InterpolationMode.Noperspective && interpMod == InterpolationMode.Centroid)
                                member.interpolation = InterpolationMode.NoperspectiveCentroid;
                            else if (interpType == InterpolationMode.Noperspective && interpMod == InterpolationMode.Sample)
                                member.interpolation = InterpolationMode.NoperspectiveSample;
                            else
                                member.interpolation = interpType;
                            member.type = lexer.TokenText;

                            lexer.GetToken();
                            member.name = lexer.TokenText;

                            lexer.GetToken();
                            if (lexer.token == ':')
                            {
                                lexer.GetToken();
                                member.semantic = lexer.TokenText;
                                lexer.GetToken();
                            }
                            if (lexer.token == ';')
                                lexer.GetToken();

                            def.members.Add(member);

                            lexer.GetToken();
                        }
                        lexer.GetToken(); // ;
                        structs.Add(def);
                    }
                    else if (lexer.Peek() == Token.ID)
                    {
                        Lexer saved = new Lexer("");
                        lexer.SaveState(saved);
                        lexer.GetToken();
                        bool isFunc = (lexer.token == Token.ID && lexer.Peek() == '(');
                        lexer.RestoreState(saved);

                        if (isFunc)
                        {
                            FunctionDef func = new FunctionDef();
                            func.returnType = text;
                            func.attributes.AddRange(pendingAttrs);
                            func.numThreadsX = pendingNX;
                            func.numThreadsY = pendingNY;
                            func.numThreadsZ = pendingNZ;
                            ClearPending();

                            lexer.GetToken(); // func name
                            func.name = lexer.TokenText;

                            lexer.GetToken(); // (
                            lexer.GetToken();
                            while (lexer.token != ')' && lexer.token != Token.EOF)
                            {
                                FunctionParam param = new FunctionParam();
                                for (;;)
                                {
                                    string t = lexer.TokenText;
                                    if (t == "in") { param.isIn = true; lexer.GetToken(); }
                                    else if (t == "out") { param.isOut = true; lexer.GetToken(); }
                                    else if (t == "inout") { param.isInOut = true; lexer.GetToken(); }
                                    else if (t == "const") { param.isConst = true; lexer.GetToken(); }
                                    else if (t == "uniform") { param.isUniform = true; lexer.GetToken(); }
                                    else break;
                                }
                                param.type = lexer.TokenText;

                                lexer.GetToken();
                                if (lexer.token == ':' || lexer.token == ',' || lexer.token == ')')
                                {
                                    if (lexer.token == ':')
                                    {
                                        lexer.GetToken();
                                        param.semantic = lexer.TokenText;
                                        lexer.GetToken();
                                    }
                                }
                                else
                                {
                                    param.name = lexer.TokenText;
                                    lexer.GetToken();
                                    if (lexer.token == ':')
                                    {
                                        lexer.GetToken();
                                        param.semantic = lexer.TokenText;
                                        lexer.GetToken();
                                    }
                                }

                                if (lexer.token == '=')
                                {
                                    int depth = 1;
                                    while (depth > 0 && lexer.GetToken() != Token.EOF)
                                    {
                                        if (lexer.token == '(') depth++;
                                        else if (lexer.token == ')') depth--;
                                    }
                                    lexer.GetToken();
                                }

                                func.parameters.Add(param);

                                if (lexer.token == ',')
                                    lexer.GetToken();
                            }

                            lexer.GetToken(); // after )
                            if (lexer.token == ':')
                            {
                                lexer.GetToken();
                                func.semantic = lexer.TokenText;
                                lexer.GetToken();
                            }

                            if (lexer.token == '{')
                            {
                                int depth = 1;
                                while (depth > 0 && lexer.GetToken() != Token.EOF)
                                {
                                    if (lexer.token == '{') depth++;
                                    else if (lexer.token == '}') depth--;
                                }
                            }

                            functions.Add(func);
                        }
                    }
                }
            }
        }
    }
}
