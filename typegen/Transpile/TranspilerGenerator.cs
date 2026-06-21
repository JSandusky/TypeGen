using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace typegen.Transpile
{
    public abstract class TranspilerGenerator
    {
        public abstract void ExportType(TranspileFileTarget target, TranspileType type);
    }
}
