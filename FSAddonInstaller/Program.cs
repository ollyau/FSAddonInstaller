using Microsoft.Win32;
using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace FSAddonInstaller {
    class Program {
        /// <summary>
        /// Program states
        /// </summary>
        private enum State {
            INSTALL,
            UNINSTALL
        }

        /* String constants */
        private const string WELCOME_MESSAGE = "Generic FSX Addon Installer/Uninstaller\r\nBy Orion Lyau\r\n\r\nUsage:\r\n[directory]\t- Installs files from the specified directory into FSX.\r\n\t\t- Generates an XML file for uninstallation.\r\n[*.xml]\t\t- Triggers uninstall based on XML configuration file.\r\n\r\n";
        private const string EXIT_MESSAGE = "\r\nPress any key to close...";
        private const string INVALID_DIR_OR_FILE = "ERROR: An invalid directory or file was passed as an argument.";
        private const string INVALID_NUM_ARGS = "ERROR: Invalid number of arguments.";
        private const string XML_OUTPUT = "\r\n\r\n\tSource directory: {0}\r\n\tDestination directory: {1}\r\n\tTime stamp: {2}\r\n\tProcessed: {3} files, {4} directories\r\n\tSkipped {5} file(s).\r\n\tBacked up {6} file(s).\r\n\r\n";
        private const string FSX_REG = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\microsoft games\flight simulator\10.0";

        /* Program members */
        private State state;
        private string path;
        private string fsxDirectory;
        private int directoryCount;
        private int fileCount;
        private int skipCount;
        private int backupCount;
        private XDocument xmlConfig;
        private bool backupAndReplaceEnabled;

        /// <summary>
        /// Main program entry point
        /// </summary>
        /// <param name="args">Command line arguments</param>
        static void Main(string[] args) {
            Console.WriteLine(WELCOME_MESSAGE);

            if (args.Length != 1) {
                Console.WriteLine(INVALID_NUM_ARGS);
            } else {
                try {
                    Program p = new Program(args[0]);
                    p.run();
                } catch (ArgumentException e) {
                    Console.WriteLine(e.Message);
                }
            }
            
            Console.WriteLine(EXIT_MESSAGE);
            Console.ReadKey();
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="path">The path to the directory or XML log file</param>
        Program(string path) {
            // Argument checks
            if (Directory.Exists(path)) {
                state = State.INSTALL;
            } else if (File.Exists(path) && path.EndsWith(".xml")) {
                state = State.UNINSTALL;
            } else {
                throw new ArgumentException(INVALID_DIR_OR_FILE);
            }

            // Class member initialisation
            path = null;
            fsxDirectory = null;
            directoryCount = 0;
            fileCount = 0;
            skipCount = 0;
            backupCount = 0;
            xmlConfig = null;
            backupAndReplaceEnabled = true;

            // Get FSX directory
            fsxDirectory = getFsxDirectory();
            Console.WriteLine("FSX Directory:\t" + fsxDirectory + "\r\n");
        }

        /// <summary>
        /// Runs the program
        /// </summary>
        public void run() {
            switch (state) {
                case State.INSTALL:
                    install(path);
                    break;
                case State.UNINSTALL:
                    uninstall(path);
                    break;
                default:
                    return;
            }
        }

        /// <summary>
        /// Installs the addon directory structure
        /// </summary>
        /// <param name="path">The directory to install</param>
        private void install(string path) {
            // Install setup
            xmlConfig = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XComment("\tThis is an uninstallation configration file. Please do not modify.\t"),
                new XElement("files")
            );

            getFilesAndDirectories(path);

            // Install status
            Console.WriteLine("\r\nProcessed {0} files, {1} directories.\r\n", fileCount, directoryCount);
            Console.WriteLine("Files skipped: " + skipCount);
            Console.WriteLine("Files backed up: " + backupCount);
            Console.WriteLine("Saving uninstallation configuration file.");

            // Save the XML log
            xmlConfig.AddFirst(new XComment(String.Format(XML_OUTPUT, path, fsxDirectory, DateTime.Now.ToString("F"), fileCount, directoryCount, skipCount, backupCount)));
            xmlConfig.Save("UninstallConfig_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff") + ".xml");
        }

        /// <summary>
        /// Uninstalls an addon using a given XML log
        /// </summary>
        /// <param name="path">The path to the XML file</param>
        private void uninstall(string path) {
            XmlDocument input;
            XmlNodeList nodes;

            input = new XmlDocument();
            input.Load(path);
            nodes = input.GetElementsByTagName("file");

            for (int i = nodes.Count - 1; i >= 0; i--) {
                // Determine the file to delete
                string currentFile = nodes[i].Attributes["location"].Value;

                // Delete the file if it exists
                if (File.Exists(currentFile)) {
                    Console.WriteLine("Delete file:\t" + currentFile);
                    File.Delete(currentFile);
                    fileCount++;
                }

                // Restore the backup if one was created
                if (bool.Parse(nodes[i].Attributes["backupCreated"].Value)) {
                    if (File.Exists(currentFile + ".bak")) {
                        File.Move(currentFile + ".bak", currentFile);
                        backupCount++;
                    }
                }

                // Delete the folder the file is in if there's nothing there anymore
                string currentDirectory = Path.GetDirectoryName(currentFile);
                if (Directory.GetFileSystemEntries(currentDirectory).Length == 0) {
                    Console.WriteLine("Delete directory:\t" + currentDirectory);
                    Directory.Delete(currentDirectory);
                    directoryCount++;
                }
            }
        }

        /// <summary>
        /// Gets the FSX root directory from the registry, including a backslash
        /// </summary>
        /// <returns>FSX root directory</returns>
        private string getFsxDirectory() {
            string dir = (string)Registry.GetValue(FSX_REG, "SetupPath", null);

            if (!string.IsNullOrEmpty(dir) && !dir.EndsWith("\\")) {
                dir = dir.Insert(dir.Length, "\\");
            }

            return dir;
        }

        /// <summary>
        /// Recursively loops through source directory to look for files and calls copying function
        /// </summary>
        /// <param name="source">Root directory to start copying at</param>
        private void getFilesAndDirectories(string source) {
            foreach (string file in Directory.GetFiles(source)) {
                // Get the relative path
                string relPath = file.Substring(source.Length + 1, file.Length - (source.Length - 1));
                Console.WriteLine(Path.Combine(fsxDirectory, relPath));

                // Copy the file to the new location
                copyFile(file, Path.Combine(fsxDirectory, relPath));
                fileCount++;
            }

            foreach (string directory in Directory.GetDirectories(source)) {
                directoryCount++;
                getFilesAndDirectories(directory);
            }
        }

        /// <summary>
        /// Copies a file
        /// </summary>
        /// <param name="source">The full path to the source file</param>
        /// <param name="destination">The full path to the destination file</param>
        private void copyFile(string source, string destination) {
            bool backupCreated = false;

            // Status output
            Console.WriteLine("Copy:\r\n{0}\r\n{1}", source, destination);

            // Create directory if it doesn't exist
            if (!Directory.Exists(Path.GetDirectoryName(destination))) {
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
            }

            // Actual copying takes place here
            if (!File.Exists(destination)) {
                File.Copy(source, destination);
            } else if (backupAndReplaceEnabled) {
                // make a backup if it doesn't exist
                if (!File.Exists(destination + ".bak")) {
                    File.Move(destination, destination + ".bak");
                    backupCreated = true;
                    backupCount++;
                }

                // delete original file
                if (File.Exists(destination)) {
                    File.Delete(destination);
                }

                // replace the old file
                File.Copy(source, destination);
            } else {
                skipCount++;
            }

            // Log the operation
            xmlConfig.Element("files").Add(new XElement("file", new XAttribute("location", destination), new XAttribute("backupCreated", backupCreated)));
        }
    }
}