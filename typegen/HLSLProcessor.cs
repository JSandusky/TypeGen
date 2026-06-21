using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using STB;

namespace typegen
{
    public class ShaderResource
    {
        public int register = -1;
        public string name = "";
    }

    public class BufferEntry { 
        public string type;
        public string name;
        public int arraySize = 1;
        public string constantSize = "";
        public int offset = -1;
    }
    

    public class ConstantBuffer : ShaderResource
    {

    }

    public class Buffer
    {

    }

    public class Sampler
    {

    }

    public enum TextureType { 
        Buffer,
        Texture1D,
        Texture2D,
        Texture3D,
        Texture2DArray,
        TextureCube,
        TextureCubeArray,
        Texture2DMS,
        Texture2DMSArray
    }

    public class Texture : ShaderResource
    {
        public TextureType type;
    }

    public class HLSLProcessor
    {
        List<ConstantBuffer> cbuffers = new List<ConstantBuffer>();
        List<Buffer> buffers = new List<Buffer>();
        List<Sampler> samplers = new List<Sampler>();
        List<Texture> textures = new List<Texture>();

        /// <summary>
        /// Processes a segment of code looking for cbuffers, samplers, buffers, and textures
        /// </summary>
        public void Process(string code)
        {
            Lexer lexer = new Lexer(code);

            while (lexer.GetToken() != Token.EOF)
            {
                if (lexer.token == Token.ID)
                {
                    if (lexer.TokenText == "cbuffer")
                    {
                        lexer.GetToken();
                        ConstantBuffer buff = new ConstantBuffer();

                        if (lexer.token == Token.ID)
                        {
                            buff.name = lexer.TokenText;
                            lexer.GetToken();
                        }
                        if (lexer.token == ':')
                        {
                            lexer.GetToken();
                            if (lexer.token == Token.ID && lexer.TokenText == "register")
                            {
                                lexer.GetToken(); //(
                                lexer.GetToken(); // c0
                                buff.register = int.Parse(lexer.TokenText.Substring(1));
                                lexer.GetToken(); //)
                            }
                        }

                        lexer.GetToken(); // {
                        while (lexer.token != '}')
                        {
                            lexer.GetToken();

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
                        }
                    }
                    else if (lexer.TokenText.StartsWith("Texture") || lexer.TokenText == "Buffer")
                    {
                        Texture tex = new Texture();
                        tex.type = (TextureType)Enum.Parse(typeof(TextureType), lexer.TokenText);
                        lexer.GetToken();
                        tex.name = lexer.TokenText;
                        lexer.GetToken();
                        if (lexer.token == ':')
                        {
                            lexer.GetToken(); //register
                            lexer.GetToken(); // )
                            lexer.GetToken();
                            tex.register = int.Parse(lexer.TokenText.Substring(1));
                            lexer.GetToken(); // )
                        }
                    }
                    else if (lexer.TokenText.StartsWith("RW"))
                    {

                    }
                    else if (lexer.TokenText == "StructuredBuffer")
                    {

                    }
                }
            }
        }
    }
}
