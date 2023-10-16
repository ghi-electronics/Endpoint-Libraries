using System.IO;
using GHIElectronics.Endpoint.Core;

namespace GHIElectronics.Endpoint {
    public static class FileSystem {

        //public static string Mount(string deviceName, string mountedName = "") => Mount(deviceName, -1, mountedName);

        //public static string Mount(string deviceName, int deviceId) => Mount(deviceName, deviceId, "");

        const string ROOT_FS_FOLDER = "/root/.media";
        public static string Mount(string deviceName) {

            var rnd = new Random();
            var num = rnd.Next();

            var arg = string.Empty;
            Script script;

            if (!Directory.Exists(ROOT_FS_FOLDER)) {

                arg = string.Format("{0}", ROOT_FS_FOLDER);
                script = new Script("mkdir", "/root", arg);

                script.Start();

                if (script.Error.Length > 0) {
                    throw new Exception(script.Error);
                }
            }

            if (deviceName == null || deviceName.Length == 0)
                throw new Exception("Invalid argument");

            var folder_name = string.Format("media{0}", num);

           

            var dir = ROOT_FS_FOLDER + "/" + folder_name;

            if (Directory.Exists(dir)) {
                throw new Exception("The directory is already exist. Use different mountedName");
            }

            if (!Directory.Exists(dir)) {
                arg = string.Format("{0}", dir);
                script = new Script("mkdir", "/root", arg);

                script.Start();
            }

            if (Directory.Exists(dir)) {

                arg = string.Format("{0} {1}", deviceName, dir);

                script = new Script("mount", "/root", arg);

                script.Start();

                if (script.ExitCode == 0)
                    return dir;

            }
            throw new Exception("Could not mount the device " + folder_name);

        }

        //public static void Unmount(int deviceId) => Unmount(deviceId, "");

        //public static void Unmount(string specialName) => Unmount(-1, specialName);
        public static void Unmount(string path) {

            if (path == null || path.Length == 0)
                throw new Exception("Could not found the device for unmount");

            var dir = path;

            var script = new Script("umount", "/root", dir);

            script.Start();

            if (script.ExitCode == 0) {

                var arg = string.Format("-r {0}", dir);

                script = new Script("rm", "/root", arg);

                script.Start();

                if (script.ExitCode == 0) {
                    return;
                }
            }

            throw new Exception("Could not umount the device " + dir);


        }
    }
}
