using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace FSAddonInstaller
{
    class Program
    {
        static string fsxDirectory = null;

        static string rootDirectory = null;
        static int fileCount = 0;
        static int directoryCount = 0;
        static int skipCount = 0;
        static int backupCount = 0;

        static XDocument xmlConfig;

        static bool backupAndReplaceEnabled = true;

        static string getFsxDirectory()
        {
            // Get FSX path from registry
            string dir = (string)Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\microsoft games\flight simulator\10.0", "SetupPath", null);

            // dev
            //string dir = @"C:\Users\Orion\Desktop\tempfsx";

            // Add ending slash
            if (!string.IsNullOrEmpty(dir) && !dir.EndsWith("\\"))
            {
                dir = dir.Insert(dir.Length, "\\");
            }

            return dir;
        }

        /// <summary>
        /// Recursively loops through source directory to look for files and calls copying function.
        /// </summary>
        /// <param name="source">Root directory to start copying at.</param>
        static void getFilesAndDirectories(string source)
        {
            // Get ALL the files
            foreach (string s in Directory.GetFiles(source))
            {
                //Console.WriteLine("File:\t" + s);

                // Get relative path
                string temp = s.Substring(rootDirectory.Length + 1, s.Length - rootDirectory.Length - 1);
                //Console.WriteLine("Relative:\t" + temp);

                // Get new destination
                Console.WriteLine(Path.Combine(fsxDirectory, temp));

                //xmlConfig.Element("files").Add(new XElement("file", new XAttribute("location", Path.Combine(fsxDirectory, temp))));

                // Copy file
                copyFile(s, Path.Combine(fsxDirectory, temp));

                // Increment counter
                fileCount++;
            }

            // Get ALL the directories
            foreach (string s in Directory.GetDirectories(source))
            {
                //Console.WriteLine("Directory:\t" + s);

                //string temp = s.Substring(rootDirectory.Length, s.Length - rootDirectory.Length);
                //Console.WriteLine("Relative:\t" + temp);

                directoryCount++;
                getFilesAndDirectories(s);
            }
        }

        /// <summary>
        /// Copies a file.
        /// </summary>
        /// <param name="source">The full path to the source file.</param>
        /// <param name="destination">The full path to the destination file.</param>
        static void copyFile(string source, string destination)
        {
            bool backupCreated = false;

            // log copy
            Console.WriteLine("Copy:\r\n{0}\r\n{1}", source, destination);

            // create directory if it doesn't exist
            if (!Directory.Exists(Path.GetDirectoryName(destination)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
            }

            // actual copying takes place here
            if (!File.Exists(destination))
            {
                // copy the file if it doesn't already exist
                File.Copy(source, destination);
            }
            else if (backupAndReplaceEnabled)
            {
                // replacing files is enabled

                // make a backup if it doesn't exist
                if (!File.Exists(destination + ".bak"))
                {
                    File.Move(destination, destination + ".bak");
                    backupCreated = true;
                    backupCount++;
                }

                // delete original file
                if (File.Exists(destination))
                {
                    File.Delete(destination);
                }

                // replace the old file
                File.Copy(source, destination);
            }
            else
            {
                skipCount++;
            }

            //xmlConfig.Element("files").Add(new XElement("file", new XAttribute("location", destination)));
            xmlConfig.Element("files").Add(new XElement("file", new XAttribute("location", destination), new XAttribute("backupCreated", backupCreated)));
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Generic FSX Addon Installer/Uninstaller\r\nBy Orion Lyau\r\n\r\nUsage:\r\n[directory]\t- Installs files from the specified directory into FSX.\r\n\t\t- Generates an XML file for uninstallation.\r\n[*.xml]\t\t- Triggers uninstall based on XML configuration file.\r\n\r\n");

            // Get FSX directory
            fsxDirectory = getFsxDirectory();
            Console.WriteLine("FSX Directory:\t" + fsxDirectory + "\r\n");

            // The first argument is a directory
            // Start install

            if (Directory.Exists(args[0]))
            {
                // Set root directory
                rootDirectory = args[0];

                // Set up XML file
                xmlConfig = new XDocument(
                    new XDeclaration("1.0", "utf-8", "yes"),
                    new XComment("\tThis is an uninstallation configration file.  Please do not modify.\t"),
                    new XElement("files")
                    );

                // Recursion
                getFilesAndDirectories(args[0]);

                // Write metadata

                Console.WriteLine("\r\nProcessed {0} files, {1} directories.\r\n", fileCount, directoryCount);
                Console.WriteLine("Files skipped: " + skipCount);
                Console.WriteLine("Files backed up: " + backupCount);

                Console.WriteLine("Saving uninstallation configuration file.");

                // Save configuration file
                xmlConfig.AddFirst(new XComment(String.Format("\r\n\r\n\tSource directory: {0}\r\n\tDestination directory: {1}\r\n\tTime stamp: {2}\r\n\tProcessed: {3} files, {4} directories\r\n\tSkipped {5} file(s).\r\n\tBacked up {6} file(s).\r\n\r\n", args[0], fsxDirectory, DateTime.Now.ToString("F"), fileCount, directoryCount, skipCount, backupCount)));

                xmlConfig.Save("UninstallConfig_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff") + ".xml");
            }

            // The first argument is a XML file
            // Start uninstall

            if (File.Exists(args[0]) && args[0].EndsWith(".xml"))
            {
                XmlDocument input = new XmlDocument();
                input.Load(args[0]);
                XmlNodeList nodes = input.GetElementsByTagName("file");
                //int loopcount = 0;

                for (int x = nodes.Count - 1; x >= 0; x--)
                {
                    //loopcount++;

                    // determine file to delete
                    string currentFile = nodes[x].Attributes["location"].Value;

                    // delete the file if it exists
                    if (File.Exists(currentFile))
                    {
                        Console.WriteLine("Delete file:\t" + currentFile);
                        File.Delete(currentFile);
                        fileCount++;
                    }

                    // restore the backup if one was created
                    if (bool.Parse(nodes[x].Attributes["backupCreated"].Value))
                    {
                        if (File.Exists(currentFile + ".bak"))
                        {
                            File.Move(currentFile + ".bak", currentFile);
                            backupCount++;
                        }
                    }

                    // delete the folder the file is in if there's nothing there anymore
                    string currentDirectory = Path.GetDirectoryName(currentFile);
                    if (Directory.GetFileSystemEntries(currentDirectory).Length == 0)
                    {
                        Console.WriteLine("Delete directory:\t" + currentDirectory);
                        Directory.Delete(currentDirectory);
                        directoryCount++;
                    }
                }

                //Console.WriteLine("Loop count:\t{0}\r\nNode count:\t{1}", loopcount, nodes.Count);
                Console.WriteLine("\r\nDeleted {0} files, {1} directories.\r\nRestored: {2} files.\r\n", fileCount, directoryCount, backupCount);
            }

            Console.WriteLine("\r\nPress any key to close...");
            Console.ReadKey();
        }
    }
}