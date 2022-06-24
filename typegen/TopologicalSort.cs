using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace typegen
{
    public static class TopologicalSort
    {
        /// Uses a heavy weight comparison function to approximate sort topologically (there can be ... issues)
        public static List<CodeScanDB.ReflectedType> SortCostly(List<CodeScanDB.ReflectedType> types)
        {
            List<CodeScanDB.ReflectedType> copy = new List<CodeScanDB.ReflectedType>(types);
            copy.Sort((a, b) =>
            {
                var iDo = a.I_Reference_You(b);
                var youDo = b.I_Reference_You(a);
                if (iDo && !youDo)
                    return 1;
                if (youDo && !iDo)
                    return -1;
                return 0;
            });
            return copy;
        }

        /// Iteratively refines a depth based sorting
        public static List<CodeScanDB.ReflectedType> SortAnneal(List<CodeScanDB.ReflectedType> types)
        {
            // initialize everything the depth level of everything to 0
            List<int> depths = Enumerable.Repeat(0, types.Count).ToList();
            // need this so we can anneal things over iterations
            List< HashSet<CodeScanDB.ReflectedType> > touches = new List<HashSet<CodeScanDB.ReflectedType>>();
            foreach (var t in types)
                touches.Add(new HashSet<CodeScanDB.ReflectedType>());

            var voidType = types.FirstOrDefault(t => t.typeName_ == "void");

            // TODO: can we do this without annealing? Using a heavy-weight "I reference you comparison?"
            int iters = int.MaxValue;
            do { 
                for (int i = 0; i < types.Count; ++i)
                {
                    var inherits = types[i].Everything_I_Inherit();
                    foreach (var parent in inherits)
                    {
                        int oIdx = types.IndexOf(parent);
                        depths[i] = Math.Max(depths[i], depths[oIdx] + 1);
                    }

                    foreach (var p in types[i].properties_)
                        HandleProperty(i, p, types, ref depths, ref touches);

                    foreach (var m in types[i].methods_)
                    {
                        if (m.returnType_.type_ != voidType)
                            HandleProperty(i, m.returnType_, types, ref depths, ref touches);
                        foreach (var arg in m.argumentTypes_)
                            HandleProperty(i, arg, types, ref depths, ref touches);
                    }
                }
                if (iters == int.MaxValue)
                    iters = touches.Max(s => s.Count);
                else
                    --iters;
            } while (iters > 0);

            // now build a paired value of the depth w/ TypeIndex in the list
            // this way we can sort but still have the original index on hand
            List<KeyValuePair<int,int>> depthKeys = new List<KeyValuePair<int, int>>(depths.Count);
            for (int i = 0; i < depths.Count; ++i)
                depthKeys.Add(new KeyValuePair<int,int>(depths[i], i));

            // sort by Key (depth)
            depthKeys.Sort((a,b) =>
            {
                if (a.Key < b.Key)
                    return -1;
                else if (a.Key > b.Key)
                    return 1;
                return 0;
            });

            // Extract using the value/index from the KVP that was sorted
            List<CodeScanDB.ReflectedType> output = new List<CodeScanDB.ReflectedType>();
            foreach (var idx in depthKeys)
                output.Add(types[idx.Value]);
            return output;
        }

        static void HandleProperty(int targetTypeIndex, CodeScanDB.Property p, List<CodeScanDB.ReflectedType> types, ref List<int> depths, ref List<HashSet<CodeScanDB.ReflectedType> > touches)
        {
            if (p.type_.isPrimitive_)
                return;

            int idx = types.IndexOf(p.type_);
            var record = depths[idx];
            depths[targetTypeIndex] = Math.Max(depths[idx] + 1, depths[targetTypeIndex]);
            touches[idx].Add(p.type_);

            var inherits = p.type_.Everything_I_Inherit();
            foreach (var parent in inherits)
            {
                int oIdx = types.IndexOf(parent);
                depths[idx] = Math.Max(depths[idx], depths[oIdx] + 1);
            }

            if (p.templateParameters_.Count > 0)
            {
                foreach (var tp in p.templateParameters_)
                {
                    if (!tp.IsInteger)
                        HandleProperty(targetTypeIndex, tp.Type, types, ref depths, ref touches);
                }
            }
        }

        static bool I_Reference_You(this CodeScanDB.ReflectedType t, CodeScanDB.ReflectedType ot)
        {
            if (t.Inherits(ot))
                return true;

            foreach (var p in t.properties_)
            { 
                if (I_Reference_You(p, ot))
                    return true;
            }
            foreach (var m in t.methods_)
            {
                if (I_Reference_You(m.returnType_, ot))
                    return true;
                foreach (var arg in m.argumentTypes_)
                    if (I_Reference_You(arg, ot))
                        return true;
            }
            return false;
        }

        static bool I_Reference_You(CodeScanDB.Property p, CodeScanDB.ReflectedType ot)
        {
            if (p.type_.I_Reference_You(ot))
                return true;
            foreach (var t in p.templateParameters_)
            {
                if (!t.IsInteger && I_Reference_You(t.Type, ot))
                    return true;
            }
            return false;
        }
    }
}
