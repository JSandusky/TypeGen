using System;
using System.Collections.Generic;
using System.Linq;

using Trait = System.Collections.Generic.KeyValuePair<string, string>;

namespace typegen
{
    public static class CodeScanDB_Ext
    {
        public static CodeScanDB.Method GetBase(this CodeScanDB.Method method)
        {
            if (method.declaringType_ == null)
                return method;

            if (method.accessModifiers_.HasFlag(AccessModifiers.AM_Virtual))
            {
                foreach (var baseType in method.declaringType_.baseClass_)
                {
                    var foundMethod = baseType.methods_.FirstOrDefault(m => m.SameSignature(method) && m.methodName_ == method.methodName_);
                    if (foundMethod != null)
                        return foundMethod;
                }
            }

            return method;
        }

        public static CodeScanDB.ReflectedType GetVirtualOriginType(this CodeScanDB.Method method)
        {
            CodeScanDB.Method found = method.GetBase();
            return found.declaringType_;
        }

        public static CodeScanDB.ReflectedType FirstBase(this CodeScanDB.ReflectedType type)
        {
            if (type.baseClass_.Count > 0)
                return type.baseClass_[0];
            return null;
        }

        public static CodeScanDB.ReflectedType GetRoot(this CodeScanDB.ReflectedType type)
        {
            var lastGood = type;
            var found = type.FirstBase();
            do
            {
                lastGood = found;
                found = found.FirstBase();
            } while (found != null);
            return lastGood;
        }

        public static int Depth(this CodeScanDB.ReflectedType type)
        {
            int ret = 0;
            var found = type.FirstBase();
            while (found != null)
            {
                ++ret;
                found = found.FirstBase();
            }
            return ret;
        }

        public static List<CodeScanDB.ReflectedType> DepthSortedTypes(this CodeScanDB db)
        {
            return db.types_.Values.OrderBy(o => o.Depth()).ToList();
        }

        public static bool Extends(this CodeScanDB.ReflectedType type, string baseName)
        {
            foreach (var baseT in type.baseClass_)
            {
                if (baseT.typeName_ == baseName)
                    return true;
                else if (baseT.Extends(baseName))
                    return true;
            }
            return false;
        }

        public static bool Inherits(this CodeScanDB.ReflectedType type, CodeScanDB.ReflectedType target)
        {
            foreach (var b in type.baseClass_)
            {
                if (b == target)
                    return true;
                if (b.Inherits(target))
                    return true;
            }
            return false;
        }

        public static List<CodeScanDB.ReflectedType> Everything_I_Inherit(this CodeScanDB.ReflectedType me)
        {
            List<CodeScanDB.ReflectedType> ret = new List<CodeScanDB.ReflectedType>();
            foreach (var b in me.baseClass_)
            {
                ret.AddRange(b.Everything_I_Inherit());
                ret.Add(b);
            }
            return ret;
        }

        public static bool HasTrait(this List<Trait> traits, string key)
        {
            return traits.Any(t => t.Key == key);
        }

        public static string Get(this List<Trait> bindingTraits, string key, string defaultVal = "")
        {
            for (int i = 0; i < bindingTraits.Count; ++i)
            {
                if (bindingTraits[i].Key == key)
                    return bindingTraits[i].Value;
            }
            return defaultVal;
        }

        public static bool GetBool(this List<Trait> traits, string key, bool defaultVal)
        {
            string v = traits.Get(key).ToLowerInvariant();
            if (!string.IsNullOrEmpty(v))
            {
                if (v == "true" || v == "on")
                    return true;
                else
                    return false;
            }
            return defaultVal;
        }

        public static float GetFloat(this List<Trait> traits, string key, float defaultVal)
        {
            string v = traits.Get(key);
            if (!string.IsNullOrEmpty(v))
            {
                float ret = 0.0f;
                if (float.TryParse(v, out ret))
                    return ret;
            }
            return defaultVal;
        }

        public static int GetInt(this List<Trait> traits, string key, int defaultVal)
        {
            string v = traits.Get(key);
            if (!string.IsNullOrEmpty(v))
            {
                int ret = 0;
                if (int.TryParse(v, out ret))
                    return ret;
            }
            return defaultVal;
        }

        // Double duty, finds repetitive values like:
        //      myVar="some value" myVar = "another Value" 
        // as well as:
        //      myVar = "some value; another value"
        public static List<string> GetList(this List<Trait> traits, string key)
        {
            List<string> ret = new List<string>();
            for (int i = 0; i < traits.Count; ++i)
            {
                if (traits[i].Key == key)
                {
                    var split = traits[i].Value.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (split != null && split.Count > 0)
                        ret.AddRange(split.ConvertAll(s => s.Trim()));
                }
            }
            return ret;
        }

        // used so we can fill out something like PROPERTY(myData = "min = 0.1, max = 0.5") then pull a struct Range { float min; float max; } out of it.
        static void ReflectedFill(object obj, string txt)
        {
            string[] argList = txt.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < argList.Length; ++i)
            {
                string[] terms = argList[i].Split('=');
                if (terms.Length == 2)
                {
                    terms[0] = terms[0].Trim();
                    // ~ and ` replace for quotes when filling
                    terms[1] = terms[1].Trim().Replace("~", "\"").Replace("`", "\"");

                    var prop = obj.GetType().GetField(terms[0]);
                    if (prop != null)
                    {
                        if (prop.FieldType == typeof(bool))
                        {
                            bool b = false;
                            bool.TryParse(terms[1], out b);
                            prop.SetValue(obj, b);
                        }
                        else if (prop.FieldType == typeof(int))
                        {
                            int v = 0;
                            int.TryParse(terms[1], out v);
                            prop.SetValue(obj, v);
                        }
                        else if (prop.FieldType == typeof(float))
                        {
                            float v = 0;
                            float.TryParse(terms[1], out v);
                            prop.SetValue(obj, v);
                        }
                        else if (prop.FieldType == typeof(string))
                            prop.SetValue(obj, terms[1]);
                    }
                }
            }
        }

        /// <summary>
        /// Extract a struct MyStruct
        /// MyStruct = "myVar = False, myVar2 = 1.0, myString = 'Some text but don`t do this'"
        /// Slider = "min = 0.0, max = 1.0"
        /// </summary>
        public static T GetStruct<T>(this List<Trait> self, string traitName) where T : new()
        {
            T ret = new T();

            var found = self.FirstOrDefault(k => k.Key == traitName);
            if (found.Key == traitName)
                ReflectedFill(ret, found.Value);

            return ret;
        }

        public static T GetStruct<T>(this List<Trait> self) where T : new()
        {
            return GetStruct<T>(self, typeof(T).Name);
        }

        public static T GetClass<T>(this List<Trait> self, string traitName) where T : class, new()
        {
            var fnd = self.FirstOrDefault(k => k.Key == traitName);
            if (string.IsNullOrEmpty(fnd.Key))
                return null;

            T ret = new T();
            ReflectedFill(ret, fnd.Value);
            return ret;
        }

        public static T GetClass<T>(this List<Trait> self) where T : class, new()
        {
            return GetClass<T>(self, typeof(T).Name);
        }

        /// <summary>
        /// Extract a min-max range, colon separated.
        /// </summary>
        public static KeyValuePair<float, float> GetRange(this List<Trait> self, string traitName, KeyValuePair<float, float> defVal)
        {
            foreach (Trait t in self)
            {
                if (t.Key == traitName)
                {
                    var terms = t.Value.Split(':');
                    return new KeyValuePair<float, float>(float.Parse(terms[0]), float.Parse(terms[1]));
                }
            }
            return defVal;
        }

        /// Extract an enum, can potentially be a [Flags] enum.
        public static T GetEnum<T>(this List<Trait> list, string key) where T : struct, IConvertible
        { 
            if (!typeof(T).IsEnum)
                return new T();

            string term = list.Get(key);
            if (string.IsNullOrEmpty(term))
                return new T();

            var names = Enum.GetNames(typeof(T));
            List<T> values = new List<T>();
            foreach (T item in Enum.GetValues(typeof(T)))
                values.Add(item);
            
            string[] parts = term.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            int ret = 0;
            for (int i = 0; i < parts.Length; ++i)
            { 
                int idx = Array.IndexOf(names, parts[i]);
                if (idx >= 0)
                    ret |= (int)(object)values[idx];
            }
            return (T)(object)ret;
        }

        public static string AngelscriptSignature(this CodeScanDB.Method method)
        {
            return $"{method.ReturnTypeText().Replace("*", "@+")} {method.methodName_}{method.CallSig().Replace("*", "@+")}";
        }
    }
}
