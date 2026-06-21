using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace typegen
{
    public class DoxygenXML
    {
        string dir_;
        public DoxygenXML(string dir)
        {
            dir_ = dir;
        }

        public void GenerateSerialization(string filePath)
        {
            foreach (var fi in System.IO.Directory.EnumerateFiles(dir_))
            {
                if (fi.EndsWith(".xml"))
                {

                }
            }
        }

        public void GenerateEditorUI(string filePath)
        {

        }

        public void GenerateConsoleCMDs(string filePath)
        {

        }

        public void GenerateReflection(string filePath)
        {

        }
    }
}
