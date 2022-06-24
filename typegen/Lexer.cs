using System.Collections.Generic;
using System.Text;

namespace STB
{
    public class Token
    {
        public const int  OpenSquare = '[';
        public const int CloseSquare = ']';
        public const int  OpenCurl = '{';
        public const int CloseCurl = '}';
        public const int  OpenParen = '(';
        public const int CloseParen = ')';
        public const int Equal = '=';
        public const int Less = '<';
        public const int Greater = '>';
        public const int Plus = '+';
        public const int Minus = '-';
        public const int Mul = '*';
        public const int Div = '/';
        public const int Mod = '%';
        public const int And = '&';
        public const int Or = '|';
        public const int Xor = '^';
        public const int At = '@';
        public const int Not = '!';
        public const int Hash = '#';

        public const int EOF = 256;
        public const int ParseError = 257;    
        public const int IntLit = 258;            // 2123ui
        public const int FloatLit = 259;          // 0.5f 5e32
        public const int ID = 260;                // $roger_
        public const int DQString = 261;          // "hello bob"
        public const int SQString = 262;          // 'a' '\r'
        public const int CharLit = 263;
        public const int EqualEqual = 264;        // ==
        public const int NotEqual = 265;          // !=
        public const int LessEqual = 266;         // <=
        public const int GreaterEqual = 267;      // >=
        public const int AndAnd = 268;            // &&
        public const int OrOr = 269;              // ||
        public const int ShiftLeft = 270;         // >>
        public const int ShiftRight = 271;        // <<
        public const int PlusPlus = 272;          // ++
        public const int MinusMinus = 273;        // --
        public const int PlusEqual = 274;         // +=
        public const int MinusEqual = 275;        // -=
        public const int MulEqual = 276;          // *=
        public const int DivEqual = 277;          // /=
        public const int ModEqual = 278;          // %=
        public const int AndEqual = 279;          // &=
        public const int OrEqual = 280;           // |=
        public const int XorEqual = 281;          // ^=
        public const int Arrow = 282;             //->
        public const int EqualArrow = 283;        //=>
        public const int ShiftLeftEqual = 284;    // <<=
        public const int ShiftRightEqual = 285;   // >>=
        public const int LessGreater = 286;       // <>
        public const int TildeEqual = 287;        // ~=
        public const int Preprocessor = 288;
        public const int Comment = 289;
        public const int Numeric = 290;           // Used by satisfied
        public const int CCommentOpen = 291;  // /*
        public const int CCommentClose = 292; // */
        public const int CPPComment = 293;        // //
        public const int MustacheOpen = 294;    // {{
        public const int MustacheClose = 295;    // }}

        public const int Math = 296;
        public const int Compare = 297;
        public const int Combo = 298;
        public const int Expression = 299;
        public const int Keyword = 300;
        public const int XmlClose = 301;

        public static string ToString(int value)
        {
            switch (value)
            {
                case EOF: return "EOF";
                case ParseError: return "Parse Error";
                case ID: return "Identifier";
                case Keyword: return "Keyword";
                case IntLit: return "integer";
                case FloatLit: return "float";
                case DQString: return "DQ String";
                case SQString: return "SQ String";
                case CharLit: return "char";
                case Comment: return "Comment";
                case Preprocessor: return "Preprocessor";
                case Numeric: return "number";

                case GreaterEqual: return ">=";
                case LessEqual : return "<=";
                case EqualEqual: return "==";
                case NotEqual: return "!=";
                case OrEqual: return "|=";
                case AndEqual: return "&=";
                case XorEqual: return "^=";
                case PlusEqual: return "+=";
                case MinusEqual: return "-=";
                case MulEqual: return "*=";
                case DivEqual: return "/=";
                case ModEqual: return "%=";
                case ShiftLeft: return "<<";
                case ShiftRight: return ">>";
                case OrOr: return "||";
                case AndAnd: return "&&";
                case Arrow: return "->";
                case EqualArrow: return "=>";
                case ShiftLeftEqual: return "<<=";
                case ShiftRightEqual: return ">>=";
                case LessGreater: return "<>";
                case TildeEqual: return "~=";
                case CPPComment: return "//";
                case CCommentOpen: return "/*";
                case CCommentClose: return "*/";
                case MustacheOpen: return "{{";
                case MustacheClose: return "}};";
            }
            return ""+(char)value;
        }
    }

    public struct LexLocation
    {
        public int line_number;
        public int line_offset;
    }

    public struct LexData
    { 
        public int token;
        public object data;
        public string suffix;

        public string Text
        {
            get
            {
                switch (token)
                {
                    case Token.DQString:
                    case Token.ID:
                    case Token.Keyword:
                    case Token.SQString:
                    case Token.CharLit:
                    case Token.IntLit:
                    case Token.FloatLit:
                        return data.ToString();
                    default:
                        return Token.ToString(token);
                }
            }
        }
    }

    /// <summary>
    /// Straight-forward port of STB C Lexer to C#
    /// </summary>
    public class Lexer
    {
        /// <summary>
        /// Optional keywords for using Token.Keyword
        /// </summary>
        public HashSet<string> keywords;

        public bool parse_comments = false;
        public bool parse_preprocesser = false;
        public bool use_xml_close_token = false;
        public bool eat_xml_comments = false;

        /// <summary>
        /// Text data being parsed.
        /// </summary>
        public string input_stream;

        /// <summary>
        /// Index of EOF, length of string
        /// </summary>
        public readonly int eof;

        /// <summary>
        /// Current parse character index
        /// </summary>
        public int parse_point;

        /// <summary>
        /// Indexes of the current token's characters
        /// </summary>
        public int where_firstchar, where_lastchar;

        /// <summary>
        /// Current line in progress
        /// </summary>
        public int line_number = 0;

        /// <summary>
        /// Starting column of the line, only accurate for the first token of a line.
        /// </summary>
        public int line_start = 0;

        /// <summary>
        /// Token last returned by GetToken()
        /// </summary>
        public int token = Token.ParseError;

        /// <summary>
        /// Most recent legit float
        /// </summary>
        /// 
        public float real_number;
        /// <summary>
        /// Most recent legit integer
        /// </summary>
        public long int_number;

        /// <summary>
        /// String value of the last token
        /// </summary>
        public string string_value;

        /// <summary>
        /// Last numeric suffix encountered (ul, ull, f, etc)
        /// </summary>
        public string number_suffix;

        /// <summary>
        /// Message emitted when a ParserError result was returned
        /// </summary>
        public string error_message;

        /// <summary>
        /// When \t is encountered this is the number of spaces to mark for line_start
        /// </summary>
        public int tab_size = 4;

        /// <summary>
        /// Gets a meaningful text value from the token.
        /// </summary>
        public string TokenText
        {
            get
            {
                switch (token)
                {
                case Token.DQString:
                    return string_value;
                case Token.ID:
                case Token.Keyword:
                    return string_value;
                case Token.CharLit:
                    return "'" + string_value + "'";
                case Token.IntLit:
                    return int_number.ToString();
                case Token.FloatLit:
                    return real_number.ToString();

                case Token.EqualEqual: return "==";        // ==
                case Token.NotEqual: return "!=";          // !=
                case Token.LessEqual: return "<=";         // <=
                case Token.GreaterEqual: return ">=";      // >=
                case Token.AndAnd: return "&&";            // &&
                case Token.OrOr: return "||";              // ||
                case Token.ShiftLeft: return "<<";         // >>
                case Token.ShiftRight: return ">>";        // <<
                case Token.PlusPlus: return "++";          // ++
                case Token.MinusMinus: return "--";        // --
                case Token.PlusEqual: return "+=";         // +=
                case Token.MinusEqual: return "-=";        // -=
                case Token.MulEqual: return "*=";          // *=
                case Token.DivEqual: return "/=";          // /=
                case Token.ModEqual: return "%=";          // %=
                case Token.AndEqual: return "&=";          // &=
                case Token.OrEqual: return "|=";           // |=
                case Token.XorEqual: return "^=";          // ^=
                case Token.Arrow: return "->";             //->
                case Token.EqualArrow: return "=>";        //=>
                case Token.ShiftLeftEqual: return "<<=";    // <<=
                case Token.ShiftRightEqual: return ">>=";   // >>=
                case Token.LessGreater: return "<>";       // <>
                case Token.TildeEqual: return "~=";        // ~=
                case Token.Comment: return "//";
                case Token.CCommentOpen: return "/*";  // /*
                case Token.CCommentClose: return "*/"; // */
                case Token.CPPComment: return "//";        // //
                case Token.MustacheOpen: return "{{";    // {{
                case Token.MustacheClose: return "}}";    // }}

                default:
                    return new string((char)token, 1);
                }
            }
        }

        public Lexer(string inputStream)
        {
            input_stream = inputStream;
            eof = input_stream.Length;
            parse_point = 0;
        }

        #region internals

        public void SaveState(Lexer into)
        {
            into.parse_point = parse_point;
            into.line_number = line_number;
            into.line_start = line_start;
            into.error_message = error_message;
            into.token = token;
            into.real_number = real_number;
            into.int_number = int_number;
            into.string_value = string_value;
            into.number_suffix = number_suffix;
            into.where_firstchar = where_firstchar;
            into.where_lastchar = where_lastchar;
        }

        public void RestoreState(Lexer old)
        {
            parse_point = old.parse_point;
            line_number = old.line_number;
            line_start = old.line_start;

            error_message = old.error_message;
            token = old.token;
            real_number = old.real_number;
            int_number = old.int_number;
            string_value = old.string_value;
            number_suffix = old.number_suffix;
            where_firstchar = old.where_firstchar;
            where_lastchar = old.where_lastchar;
        }

        float ParseFloat(int p, out int q)
        {
            float value = 0.0f;
            while (input_stream[p] >= '0' && input_stream[p] <= '9')
                value = value * 10 + (input_stream[p++] - '0');

            if (input_stream[p] == '.')
            {
                float powten = 1, addend = 0;
                ++p;

                while (input_stream[p] >= '0' && input_stream[p] <= '9')
                {
                    addend = addend * 10 + (input_stream[p++] - '0');
                    powten *= 10;
                }

                value += addend / powten;
            }

            if (input_stream[p] == 'e' || input_stream[p] == 'E')
            {
                int sign = input_stream[p+1] == '-' ? 1 : 0;
                int exponent = 0;
                float pow10 = 1;
                p += 1 + sign;

                while (input_stream[p] >= '0' && input_stream[p] <= '9')
                    exponent = exponent * 10 + (input_stream[p] - '0');

                while (exponent-- > 0)
                    pow10 *= 10;

                if (sign > 0)
                    value /= pow10;
                else
                    value *= pow10;
            }

            q = p;
            return value;
        }

        int ParseString(int p)
        {
            int q = p;
            int start = p;
            char delim = input_stream[p];

            while (p != eof && input_stream[p] != delim)
            {
                int n;
                if (input_stream[p] == '\\')
                {
                    if (!char.IsLetter(input_stream[q]))
                    {
                        error_message = $"Illegal escape token '{input_stream[q]}'";
                        return DoRet(Token.ParseError, p, q + 1);
                    }
                    
                    n = input_stream[p];
                    ++p;
                }
                else
                    n = input_stream[p++];

            }

            return 0;
        }

        void EatSuffixes(ref int p)
        {
            number_suffix = "";
            while (p != eof && char.IsLetter(input_stream[p]))
            {
                number_suffix += input_stream[p];
                ++p;
            }
        }

        int DoRet(int v, int startParse, int endParse) { 
            where_firstchar = startParse;
            where_lastchar = endParse;

            string_value = input_stream.Substring(startParse, endParse - startParse);
            parse_point = endParse; 

            if (v == Token.ID && keywords != null && keywords.Contains(string_value))
                v = Token.Keyword;
            
            token = v;
            return v; 
        }

        internal static bool SatisfiesNumeric(int token, int checkToken)
        {
            return token == checkToken || 
                (checkToken == Token.Numeric && (token == Token.IntLit || token == Token.FloatLit)) ||
                (checkToken == Token.Compare && (
                    token == Token.Greater || 
                    token == Token.Less ||
                    token == Token.GreaterEqual ||
                    token == Token.LessEqual ||
                    token == Token.NotEqual ||
                    token == Token.EqualEqual
                )) ||
                (checkToken == Token.Math && (
                    token == Token.Plus ||
                    token == Token.Minus ||
                    token == Token.Mul ||
                    token == Token.Div ||
                    token == Token.Mod ||
                    token == Token.Or ||
                    token == Token.And ||
                    token == Token.Xor ||
                    token == Token.PlusEqual ||
                    token == Token.MinusEqual ||
                    token == Token.MulEqual ||
                    token == Token.DivEqual ||
                    token == Token.ModEqual ||
                    token == Token.OrEqual ||
                    token == Token.AndEqual ||
                    token == Token.XorEqual ||
                    token == Token.Equal
                )) ||

                (checkToken == Token.Combo && (
                    token == Token.AndAnd || 
                    token == Token.OrOr
                )) ||

                (checkToken == Token.Expression && (
                    
                    token == Token.Greater ||
                    token == Token.Less ||
                    token == Token.GreaterEqual ||
                    token == Token.LessEqual ||
                    token == Token.NotEqual ||
                    token == Token.EqualEqual ||

                    token == Token.Plus ||
                    token == Token.Minus ||
                    token == Token.Mul ||
                    token == Token.Div ||
                    token == Token.Mod ||
                    token == Token.Or ||
                    token == Token.And ||
                    token == Token.Xor ||
                    token == Token.PlusEqual ||
                    token == Token.MinusEqual ||
                    token == Token.MulEqual ||
                    token == Token.DivEqual ||
                    token == Token.ModEqual ||
                    token == Token.OrEqual ||
                    token == Token.AndEqual ||
                    token == Token.XorEqual ||
                    token == Token.Equal ||

                    token == Token.AndAnd ||
                    token == Token.OrOr
                ));

        }

        LexData CreateLexData()
        {
            if (token == Token.IntLit)
                return new LexData { token = token, data = int_number, suffix = number_suffix };
            else if (token == Token.FloatLit)
                return new LexData { token = token, data = real_number, suffix = number_suffix };
            else
                return new LexData { token = token, data = string_value, suffix = null };
        }

        bool StreamMatches(int tokenStart, string text)
        {
            for (int i = 0; i < text.Length; ++i)
            {
                if (tokenStart + i == eof)
                    return false;

                if (input_stream[tokenStart + i] != text[i])
                    return false;
            }
            return true;
        }

        #endregion

        #region public API

        Lexer stateSave;
        public void SaveState()
        {
            stateSave = new Lexer("");
            SaveState(stateSave);
        }

        public void RestoreState()
        {
            if (stateSave != null)
                RestoreState(stateSave);
        }

        /// <summary>
        /// Takes a look at the next character
        /// </summary>
        /// <returns>Next token</returns>
        public int Peek()
        {
            Lexer old = new Lexer("");
            SaveState(old);

            int ret = GetToken();

            RestoreState(old);

            return ret;
        }

        public string PeekText()
        {
            Lexer old = new Lexer("");
            SaveState(old);
            GetToken();
            string ret = TokenText;
            RestoreState(old);
            return ret;
        }

        /// <summary>
        /// Performs a peek, but also grabs the start-location of the token we peaked at.
        /// </summary>
        public int Peek(out int tokenParsePoint)
        {
            Lexer old = new Lexer("");
            SaveState(old);

            int ret = GetToken();
            tokenParsePoint = where_firstchar;

            RestoreState(old);

            return ret;
        }

        /// <summary>
        /// Grabs the next token (Token.EOF or Token.ParseError possibly)
        /// </summary>
        /// <returns>Token found</returns>
        public int GetToken()
        {            
            // these are always wiped, if you care about you have to check it yourself
            line_start = 0;
            error_message = null;

            int p = parse_point;

            for (;;)
            {
                bool bumbStartInfo = false;
                while (p != eof)
                { 
                    if (!char.IsWhiteSpace(input_stream[p]))
                        break;
                    if (input_stream[p] == '\n')
                    {
                        ++line_number;
                        line_start = 0;
                        bumbStartInfo = true;
                    }
                    else if (bumbStartInfo)
                        line_start += '\t' == input_stream[p] ? tab_size : 1;
                    ++p;
                }

                // comment lex
                if (!parse_comments)
                {
                    // CPP comments
                    if (StreamMatches(p, "//"))
                    {
                        while (p != eof && input_stream[p] != '\r' && input_stream[p] != '\n')
                            ++p;
                        continue;
                    }

                    /* C style comments */
                    if (StreamMatches(p, "/*"))
                    {
                        int start = p;
                        p += 2;
                        while (p != eof && !StreamMatches(p, "*/"))
                            ++p;
                        if (p == eof)
                        { 
                            error_message = "Unexpected end of comment";
                            return DoRet(Token.ParseError, p, p + 1);
                        }
                        p += 2;
                        continue;
                    }
                }

                if (eat_xml_comments)
                {
                    if (StreamMatches(p, "<!--"))
                    {
                        p += 4;
                        while (p != eof && !StreamMatches(p, "-->"))
                            ++p;

                        if (p == eof)
                        {
                            error_message = "Unexpected end of comment";
                            return DoRet(Token.ParseError, p, p + 1);
                        }

                        p += 3;
                        continue;
                    }
                }

                // Skip preprocessor #region #endregion
                if (!parse_preprocesser && p != eof && input_stream[p] == '#')
                {
                    while (p != eof && input_stream[p] != '\r' && input_stream[p] != '\n')
                        ++p;
                    continue;
                }

                break;
            }

            if (p == eof)
                return DoRet(Token.EOF, p, p);

            switch (input_stream[p])
            { 
                default:
                    if (char.IsLetter(input_stream[p]) || input_stream[p] == '$')
                    {
                        int q = p;
                        while (q != eof && (char.IsLetter(input_stream[q]) || input_stream[q] == '$' || input_stream[q] == '_' || char.IsDigit(input_stream[q])))
                            ++q;
                        
                        return DoRet(Token.ID, p, q);
                    }
                single_char:
                    return DoRet(input_stream[p], p,  p + 1);
                case '"': // string
                {
                    int q = p + 1;
                    while (q != eof && input_stream[q] != '"')
                    {
                        if (input_stream[q] == '\\' && input_stream[q+1] == '"')
                            q++;
                        q++;
                    }
                    return DoRet(Token.DQString, p, q + 1);
                }
                case '\'': // char literal
                {
                    int q = p;
                    if (input_stream[q+1] == '\\')
                    {
                        q++;
                        if (input_stream[q+1] == 't')
                        { 
                            int_number = '\t';
                            return DoRet(Token.CharLit, p, q + 2);
                        }
                        else if (input_stream[q+1] == 'r')
                        {
                            int_number = '\t';
                            return DoRet(Token.CharLit, p, q + 2);
                        }
                        else if (input_stream[q + 1] == 'n')
                        {
                            int_number = '\t';
                            return DoRet(Token.CharLit, p, q + 2);
                        }
                    }
                    int_number = input_stream[p+1];
                    return DoRet(Token.CharLit, p, q + 2);
                }
                case '+':
                    if (p + 1 != eof)
                    {
                        if (input_stream[p+1] == '+') return DoRet(Token.PlusPlus,  p, p + 2);
                        if (input_stream[p+1] == '=') return DoRet(Token.PlusEqual, p, p + 2);
                    }
                    goto single_char;
                case '-':
                    if (p + 1 != eof)
                    {
                        if (input_stream[p + 1] == '-') return DoRet(Token.MinusMinus, p, p + 2);
                        if (input_stream[p + 1] == '=') return DoRet(Token.MinusEqual, p, p + 2);
                        if (input_stream[p + 1] == '>') return DoRet(Token.Arrow, p, p + 2);
                    }
                    goto single_char;
                case '&':
                    if (p + 1 != eof)
                    {
                        if (input_stream[p + 1] == '&') return DoRet(Token.AndAnd,   p, p + 2);
                        if (input_stream[p + 1] == '=') return DoRet(Token.AndEqual, p, p + 2);
                    }
                    goto single_char;
                case '|':
                    if (p + 1 != eof)
                    {
                        if (input_stream[p + 1] == '|') return DoRet(Token.OrOr,    p, p + 2);
                        if (input_stream[p + 1] == '=') return DoRet(Token.OrEqual, p, p + 2);
                    }
                    goto single_char;
                case '=':
                    if (p + 1 != eof)
                    {
                        if (input_stream[p + 1] == '=') return DoRet(Token.EqualEqual, p, p + 2);
                        if (input_stream[p + 1] == '>') return DoRet(Token.EqualArrow, p, p + 2);
                    }
                    goto single_char;
                case '!':
                    if (p + 1 != eof)
                    {
                        if (input_stream[p + 1] == '=') return DoRet(Token.NotEqual, p, p + 2);
                    }
                    goto single_char;
                case '^':
                    if (p + 1 != eof)
                    {
                        if (input_stream[p + 1] == '=') return DoRet(Token.XorEqual, p, p + 2);
                    }
                    goto single_char;
                case '%':
                    if (p + 1 != eof)
                    {
                        if (input_stream[p + 1] == '=') return DoRet(Token.ModEqual, p, p + 2);
                    }
                    goto single_char;
                case '*':
                    if (p + 1 != eof)
                    {
                        if (input_stream[p + 1] == '=') return DoRet(Token.MulEqual, p, p + 2);
                        if (parse_comments && input_stream[p + 1] == '/') return DoRet(Token.CCommentClose, p, p + 2);
                    }
                    goto single_char;
                case '/':
                    if (p + 1 != eof)
                    {
                        if (input_stream[p + 1] == '=') return DoRet(Token.DivEqual, p, p + 2);
                        if (input_stream[p + 1] == '/') return DoRet(Token.CPPComment, p, p + 2);
                        if (parse_comments && input_stream[p + 1] == '*') return DoRet(Token.CCommentOpen, p, p + 2);
                        if (use_xml_close_token && input_stream[p + 1] == '>') return DoRet(Token.XmlClose, p, p + 2);
                    }
                    goto single_char;
                case '<':
                    if (p + 1 != eof)
                    {
                        if (input_stream[p + 1] == '<')
                        {
                            if (p + 2 != eof && input_stream[p + 2] == '=')
                                return DoRet(Token.ShiftLeftEqual, p, p + 3);
                            return DoRet(Token.ShiftLeft, p, p + 2);
                        }
                        if (input_stream[p + 1] == '=') return DoRet(Token.LessEqual, p, p + 2);
                        if (input_stream[p + 1] == '>') return DoRet(Token.LessGreater, p, p + 2);
                    }
                    goto single_char;
                case '>':
                    if (p + 1 != eof)
                    {
                        if (input_stream[p + 1] == '>') {
                            if (p+2 != eof && input_stream[p+2] == '=')
                                return DoRet(Token.ShiftRightEqual, p, p + 3);
                            return DoRet(Token.ShiftRight, p, p + 2);
                        }
                        if (input_stream[p + 1] == '=') return DoRet(Token.GreaterEqual, p, p + 2);
                    }
                    goto single_char;
                case '~':
                    if (p + 1 != eof && input_stream[p+1] == '=') return DoRet(Token.TildeEqual, p, p + 2);
                    goto single_char;
                case '{':
                    if (p + 1 != eof && input_stream[p + 1] == '{') return DoRet(Token.MustacheOpen, p, p + 2);
                    goto single_char;
                case '}':
                    if (p + 1 != eof && input_stream[p + 1] == '}') return DoRet(Token.MustacheClose, p, p + 2);
                    goto single_char;


                case'0': case '1': case '2': case '3': case '4': case '5': case'6': case '7': case '8': case '9':
                // floats
                {
                    int q = p;
                    while (q != eof && input_stream[q] >= '0' && input_stream[q] <= '9')
                        ++q;

                    if (q != eof)
                    {
                        if (input_stream[q] == '.' || char.ToLowerInvariant(input_stream[q]) == 'e')
                        { 
                            real_number = ParseFloat(p, out q);

                            // eat a possible 'f' suffix
                            EatSuffixes(ref q);
                            return DoRet(Token.FloatLit, p, q);
                        }
                    }
                }

                // integers
                {
                    int n = 0;
                    int q = p;
                    while (q != eof && (input_stream[q] >= '0' && input_stream[q] <= '9'))
                    { 
                        n = n * 10 + (input_stream[q] - '0');
                        ++q;
                    }

                    int_number = n;
                    EatSuffixes(ref q);
                    return DoRet(Token.IntLit, p, q);
                }
            }
        }

        /// <summary>
        /// Calculates the line and column at the current parse location.
        /// </summary>
        public LexLocation GetLocation() { return GetLocation(parse_point); }

        /// <summary>
        /// Determines the line and column of a linear character index.
        /// </summary>
        public LexLocation GetLocation(int where)
        {
            int line_number = 1;
            int char_offset = 0;

            int p = 0;
            while (p < eof && p < where)
            {
                if (input_stream[p] == '\n')
                {
                    line_number += 1;
                    char_offset = 0;
                    p += 1;
                }
                else if (input_stream[p] == '\r')
                {
                    p += 2; // skip over \n for \r\n
                    line_number += 1;
                }
                else
                {
                    p += 1;
                    char_offset += 1;
                }
            }
            return new LexLocation { line_number = line_number, line_offset = char_offset };
        }

        /// <summary>
        /// True if we're at the end.
        /// </summary>
        public bool IsEOF { get { return token == Token.EOF; } }

        /// <summary>
        /// Grabs a delimited block as text (such as { ... })
        /// </summary>
        /// <returns>The captured text, possibly null</returns>
        public string EatBlock(char openToken, char closeToken)
        {
            int depth = 0;
            bool inQuote = false;
            bool inChar = false;
            int q = parse_point;
            StringBuilder sb = new StringBuilder();

            bool haveOpened = q > 0 ? input_stream[q-1] == openToken : false;

            while (q != eof)
            {
                if (input_stream[q] == '\n')
                    ++line_number;

                if (input_stream[q] == openToken && inQuote == false && inChar == false)
                { 
                    haveOpened = true;
                    ++depth;
                    if (depth > 1) sb.Append(openToken);
                    
                    ++q;
                    continue;
                }
                else if (input_stream[q] == closeToken && inQuote == false && inChar == false)
                {
                    --depth;
                    if (depth > 0) sb.Append(closeToken);
                    else
                    {
                        parse_point = q + 1;
                        return sb.ToString();
                    }
                }
                else if (input_stream[q] == '\"')
                    inQuote = !inQuote;
                else if (input_stream[q] == '\"')
                    inChar = !inChar;
                else if (input_stream[q] == '\\')
                {
                    if (input_stream[q+1] == '\"' || input_stream[q+1] == '\'')
                    { 
                        sb.Append(input_stream[q]);
                        sb.Append(input_stream[q+1]);
                        q += 2;
                        continue;
                    }
                }

                if (haveOpened)
                    sb.Append(input_stream[q]);
                ++q;
            }

            if (haveOpened)
                return sb.ToString();
            return null;
        }

        public void EatUntil(string stringVal)
        {
            while (!string_value.Equals(stringVal))
                GetToken();
        }

        /// <summary>
        /// Grabs a stream of tokens and data until hitting the specified token.
        /// EOF is valid
        /// </summary>
        public List<LexData> EatStream(int untilToken)
        {
            List<LexData> ret = new List<LexData>();

            for (;;)
            { 
                int tk = GetToken();
                if (tk == untilToken || tk == Token.EOF || tk == Token.ParseError)
                    return ret;

                ret.Add(CreateLexData());
            }
        }

        /// <summary>
        /// Grabs a (possibly looping) string of tokens.
        /// ie. 
        ///     new int[] { Token.Numeric, ',' }, true } 
        ///     grabs a string of 5, 7, 9, 10
        /// State is left at the failing token, not rolled back
        /// </summary>
        public List<LexData> EatSequence(int[] tokens, bool loop)
        {
            List<LexData> ret = new List<LexData>();

            int i = 0;
            for (;;)
            {
                if (loop && i > tokens.Length)
                    i = 0;

                int tk = GetToken();
                if (tk != tokens[i] || tk == Token.EOF || tk == Token.ParseError)
                {
                    return ret;
                }

                ret.Add(CreateLexData());
                ++i;
            }
        }

        /// <summary>
        /// Check we're going to receive a string of tokens.
        /// </summary>
        public bool Satisfies(int[] tokenString)
        {
            if (tokenString == null || tokenString.Length == 0)
                return false;

            Lexer old = new Lexer("");
            SaveState(old);

            for (int i = 0; i < tokenString.Length; ++i)
            {
                int tk = GetToken();
                if (!SatisfiesNumeric(tk, tokenString[i]))
                {
                    RestoreState(old);
                    return false;
                }
            }

            RestoreState(old);
            return true;
        }

        /// <summary>
        /// Check we're going to receive a string of tokens, and gather the data if so.
        /// </summary>
        public bool Satisifes(int[] tokenString, ref List<LexData> ret)
        {
            if (tokenString == null || tokenString.Length == 0)
                return false;

            Lexer old = new Lexer("");
            SaveState(old);

            for (int i = 0; i < tokenString.Length; ++i)
            {
                int tk = GetToken();
                if (!SatisfiesNumeric(tk, tokenString[i]))
                {
                    RestoreState(old);
                    return false;
                }
                else
                    ret.Add(CreateLexData());
            }

            return true;
        }

        /// <summary>
        /// Create a duplicate with current state.
        /// </summary>
        public Lexer Clone()
        {
            Lexer ret = new Lexer(input_stream);
            SaveState(ret);
            return ret;
        }

        #endregion

        public void EatLine()
        {
            int line = line_number;
            Lexer old = new Lexer("");
            SaveState(old);
            while (line_number == line)
            {
                SaveState(old);
                if (GetToken() == Token.EOF)
                    break;
            }
            RestoreState(old);
        }
    }

    /// <summary>
    /// Utility extension methods for common needs.
    /// </summary>
    public static class LexerUtil
    {
        /// <summary>
        /// Copies passing tokens from the given list into a new one.
        /// </summary>
        public static List<LexData> ExtractTokens(this List<LexData> data, params int[] tokens)
        {
            List<LexData> ret = new List<LexData>();

            for (int i = 0; i < data.Count; ++i)
            {
                for (int r = 0; r < tokens.Length; ++r)
                { 
                    if (data[i].token == tokens[r] || Lexer.SatisfiesNumeric(data[i].token, tokens[r]))
                        ret.Add(data[i]);
                }
            }

            return ret;

        }

        /// <summary>
        /// Removes particular tokens from the given list.
        /// </summary>
        public static void EraseTokens(this List<LexData> data, params int[] tokens)
        {
            for (int i = 0; i < data.Count; ++i)
            {
                for (int r = 0; r < tokens.Length; ++r)
                { 
                    if (data[i].token == tokens[r] || Lexer.SatisfiesNumeric(data[i].token, tokens[r]))
                    {
                        data.RemoveAt(i);
                        --i;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Converts the chain of tokens into text.
        /// </summary>
        public static string WriteText(this List<LexData> data)
        {
            StringBuilder sb = new StringBuilder();

            WriteText(data, sb);

            return sb.ToString();
        }

        /// <summary>
        /// Writes the chain of tokens into a StringBuilder as text.
        /// </summary>
        public static void WriteText(this List<LexData> data, StringBuilder sb)
        {
            for (int i = 0; i < data.Count; ++i)
            {
                LexData d = data[i];
                if (i > 0 && d.token != ';') // special handling for semicolon
                    sb.Append(' ');

                sb.Append(d.Text);
            }
        }

        public static bool IsSquareBracket(this int v)
        {
            return v == Token.OpenSquare || v == Token.CloseSquare;
        }

        public static bool IsParen(this int v)
        {
            return v == Token.OpenParen || v == Token.CloseParen;
        }

        public static bool IsCurlyBrace(this int v)
        {
            return v == Token.OpenCurl || v == Token.CloseCurl;
        }
    }

}
