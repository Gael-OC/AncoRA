using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AncorRA.AR
{
    /// <summary>
    /// A small JSON reader for multiset.json, which JsonUtility cannot read (it has a dictionary of map codes). Objects come
    /// back as Dictionary&lt;string, object&gt;, arrays as List&lt;object&gt;, numbers as double, plus string, bool and null.
    /// Strict where it matters (a malformed file is reported, never half-read); throws FormatException with the position.
    /// </summary>
    public static class PaseoMiniJson
    {
        public static object Parse(string text)
        {
            if (text == null)
                throw new FormatException("El JSON está vacío.");
            var reader = new Reader(text);
            reader.SkipSpace();
            if (reader.AtEnd)
                throw new FormatException("El JSON está vacío.");
            var value = reader.ReadValue();
            reader.SkipSpace();
            if (!reader.AtEnd)
                throw reader.Error("hay texto después del valor");
            return value;
        }

        sealed class Reader
        {
            readonly string text;
            int index;

            public Reader(string source)
            {
                text = source;
                // A BOM is not whitespace to string.Trim; editors on Windows add it.
                if (text.Length > 0 && text[0] == '﻿')
                    index = 1;
            }

            public bool AtEnd => index >= text.Length;

            public FormatException Error(string what) => new($"JSON inválido en la posición {index}: {what}.");

            public void SkipSpace()
            {
                while (index < text.Length && char.IsWhiteSpace(text[index]))
                    index++;
            }

            public object ReadValue()
            {
                SkipSpace();
                if (AtEnd)
                    throw Error("se esperaba un valor");
                char c = text[index];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9'))
                            return ReadNumber();
                        throw Error($"carácter inesperado «{c}»");
                }
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(text, index, word, 0, word.Length) != 0)
                    throw Error($"se esperaba «{word}»");
                index += word.Length;
            }

            Dictionary<string, object> ReadObject()
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                index++; // {
                SkipSpace();
                if (!AtEnd && text[index] == '}')
                {
                    index++;
                    return result;
                }
                while (true)
                {
                    SkipSpace();
                    if (AtEnd || text[index] != '"')
                        throw Error("se esperaba el nombre de un campo entre comillas");
                    string key = ReadString();
                    SkipSpace();
                    if (AtEnd || text[index] != ':')
                        throw Error("se esperaba «:»");
                    index++;
                    result[key] = ReadValue();
                    SkipSpace();
                    if (AtEnd)
                        throw Error("el objeto no se cierra");
                    if (text[index] == ',')
                    {
                        index++;
                        continue;
                    }
                    if (text[index] == '}')
                    {
                        index++;
                        return result;
                    }
                    throw Error("se esperaba «,» o «}»");
                }
            }

            List<object> ReadArray()
            {
                var result = new List<object>();
                index++; // [
                SkipSpace();
                if (!AtEnd && text[index] == ']')
                {
                    index++;
                    return result;
                }
                while (true)
                {
                    result.Add(ReadValue());
                    SkipSpace();
                    if (AtEnd)
                        throw Error("la lista no se cierra");
                    if (text[index] == ',')
                    {
                        index++;
                        continue;
                    }
                    if (text[index] == ']')
                    {
                        index++;
                        return result;
                    }
                    throw Error("se esperaba «,» o «]»");
                }
            }

            string ReadString()
            {
                var builder = new StringBuilder();
                index++; // opening quote
                while (true)
                {
                    if (AtEnd)
                        throw Error("el texto no se cierra");
                    char c = text[index++];
                    if (c == '"')
                        return builder.ToString();
                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }
                    if (AtEnd)
                        throw Error("el texto no se cierra");
                    char escaped = text[index++];
                    switch (escaped)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            if (index + 4 > text.Length ||
                                !int.TryParse(text.Substring(index, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code))
                                throw Error("escape \\u inválido");
                            builder.Append((char)code);
                            index += 4;
                            break;
                        default:
                            throw Error($"escape inválido «\\{escaped}»");
                    }
                }
            }

            double ReadNumber()
            {
                int start = index;
                while (index < text.Length && "+-0123456789.eE".IndexOf(text[index]) >= 0)
                    index++;
                string token = text.Substring(start, index - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    throw Error($"número inválido «{token}»");
                return value;
            }
        }
    }
}
