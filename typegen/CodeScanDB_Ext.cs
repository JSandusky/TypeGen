using System;
using System.Collections.Generic;
using System.Linq;

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
    }
}
