using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.Text
{
    public class JSONParser
    {
        internal class Fifo<T>
        {
            internal Fifo() : this(default(T)) { }
            private Fifo(T value)
            {
                Value = value;
            }

            internal Fifo<T> Append(T value)
            {
                return (Next = new Fifo<T>(value));
            }

            internal void CopyTo(T[] array, int start)
            {
                Fifo<T> q = this;
                int i = 0;
                while (q != null)
                {
                    if (q.Next != null)
                        array[start + i++] = q.Next.Value;
                    q = q.Next;
                }
            }

            internal readonly T Value;
            internal Fifo<T> Next;
        }

        private static readonly IDictionary<char, char> ESC = new Dictionary<char, char>();
        private StringBuilder cs = new StringBuilder();

        static JSONParser()
        {
            ESC['"'] = '"';
            ESC['\\'] = '\\';
            ESC['/'] = '/';
            ESC['b'] = '\b';
            ESC['f'] = '\f';
            ESC['n'] = '\n';
            ESC['r'] = '\r';
            ESC['t'] = '\t';
        }

        /// <summary>
        /// Converts the specified JSON string to the .NET equivalent of a JSON "value"
        /// (as defined by http://json.org/). This can be, either:
        /// null, or true/false, or a System.Double, or a System.String, or an IList(object), or an IDictionary(string, object)
        /// </summary>
        /// <param name="text">The JSON string to parse.</param>
        /// <returns>The deserialized object.</returns>
        public object Parse(string text)
        {
            int len = text.Length;
            Func<object> val = null;
            object obj = null;
            bool data = true;
            char ch = ' ';
            int at = 0;
            Func<string, Exception> error = delegate(string message)
            {
                return new Exception(String.Format("{0} at index {1}", message, at));
            };
            Func<bool> cont = delegate()
            {
                if (at < len)
                {
                    ch = text[at];
                    at += 1;
                }
                else
                    data = false;
                return data;
            };
            Func<char, bool> next = delegate(char c)
            {
                if (c != ch)
                    throw error(String.Format("Expected '{0}' instead of '{1}'", c, ch));
                if (at < len)
                {
                    ch = text[at];
                    at += 1;
                }
                else
                    data = false;
                return data;
            };
            Func<double> num = delegate()
            {
                cs.Length = 0;
                if (ch == '-')
                {
                    cs.Append('-');
                    next('-');
                }
                while ((ch >= '0') && (ch <= '9'))
                {
                    cs.Append(ch);
                    cont();
                }
                if (ch == '.')
                {
                    cs.Append('.');
                    while (cont() && (ch >= '0') && (ch <= '9'))
                        cs.Append(ch);
                }
                if ((ch == 'e') || (ch == 'E'))
                {
                    cs.Append(ch);
                    cont();
                    if ((ch == '-') || (ch == '+'))
                    {
                        cs.Append(ch);
                        cont();
                    }
                    while ((ch >= '0') && (ch <= '9'))
                    {
                        cs.Append(ch);
                        cont();
                    }
                }
                return double.Parse(cs.ToString());
            };
            Func<string> str = delegate()
            {
                int hex, i, uffff;
                cs.Length = 0;
                if (ch == '"')
                {
                    while (cont())
                    {
                        if (ch == '"')
                        {
                            cont();
                            return cs.ToString();
                        }
                        if (ch == '\\')
                        {
                            cont();
                            if (ch == 'u')
                            {
                                uffff = 0;
                                for (i = 0; i < 4; i += 1)
                                {
                                    hex = Convert.ToInt32(string.Empty + cont(), 16);
                                    uffff = uffff * 16 + hex;
                                }
                                cs.Append(Convert.ToChar(uffff));
                            }
                            else if (ESC.ContainsKey(ch))
                                cs.Append(ESC[ch]);
                            else
                                break;
                        }
                        else
                            cs.Append(ch);
                    }
                }
                throw error("Bad string");
            };
            Action space = delegate()
            {
                while (data && (ch <= ' '))
                    cont();
            };
            Func<object> word = delegate()
            {
                switch (ch)
                {
                    case 't':
                        next('t');
                        next('r');
                        next('u');
                        next('e');
                        return true;
                    case 'f':
                        next('f');
                        next('a');
                        next('l');
                        next('s');
                        next('e');
                        return false;
                    case 'n':
                        next('n');
                        next('u');
                        next('l');
                        next('l');
                        return null;
                }
                throw error(String.Format("Unexpected '{0}'", ch));
            };
            Func<IList<object>> list = delegate()
            {
                Fifo<object> qi = new Fifo<object>(), qc = qi;
                int ql = 0;
                if (ch == '[')
                {
                    next('[');
                    space();
                    if (ch == ']')
                    {
                        next(']');
                        object[] a = new object[ql];
                        qi.CopyTo(a, 0);
                        return a;
                    }
                    while (data)
                    {
                        qc = qc.Append(val());
                        ql++;
                        space();
                        if (ch == ']')
                        {
                            next(']');
                            object[] a = new object[ql];
                            qi.CopyTo(a, 0);
                            return a;
                        }
                        next(',');
                        space();
                    }
                }
                throw error("Bad array");
            };
            Func<object> hash = delegate()
            {
                IDictionary<string, object> o = new Dictionary<string, object>();
                string key;
                if (ch == '{')
                {
                    next('{');
                    space();
                    if (ch == '}')
                    {
                        next('}');
                        return o;
                    }
                    while (data)
                    {
                        key = String.Intern(str());
                        space();
                        next(':');
                        if (o.ContainsKey(key))
                            throw error(String.Format("Duplicate key \"{0}\"", key));
                        o[key] = val();
                        space();
                        if (ch == '}')
                        {
                            next('}');
                            return o;
                        }
                        next(',');
                        space();
                    }
                }
                throw error("Bad object");
            };
            val = delegate()
            {
                space();
                switch (ch)
                {
                    case '{':
                        return hash();
                    case '[':
                        return list();
                    case '"':
                        return str();
                    case '-':
                        return num();
                    default:
                        return ((ch >= '0') && (ch <= '9') ? num() : word());
                }
            };
            obj = val();
            space();
            if (data)
                throw error("Syntax error");
            return obj;
        }
    }
}