using System.IO;
using GHIElectronics.Endpoint.Core;

namespace GHIElectronics.Endpoint {
    public static class FileSystem {

        public static string Mount(string deviceName, string specialName = "") => Mount(deviceName, -1, specialName);

        public static string Mount(string deviceName, int deviceId) => Mount(deviceName, deviceId, "");
        private static string Mount(string deviceName, int deviceId, string specialName) {

            if (deviceId == -1 && (specialName == null || specialName.Length == 0))
                throw new Exception("Could not found the device for unmount");

            var folder_name = specialName == "" ? "FS" + deviceId : specialName;

            var arg = string.Format("{0}", folder_name);
            var script = new Script("mkdir", "/root", arg);

            var dir = "/root/" + folder_name;

            if (Directory.Exists(dir)) {
                throw new Exception("The directory is already exist. Use different specialName");
            }

            if (!Directory.Exists(dir)) {
                script.Start();
            }

            if (Directory.Exists(dir)) {

                arg = string.Format("{0} {1}", deviceName, folder_name);

                script = new Script("mount", "/root", arg);

                script.Start();

                if (script.ExitCode == 0)
                    return dir;

            }
            throw new Exception("Could not mount the device " + folder_name);

        }

        public static void Unmount(int deviceId) => Unmount(deviceId, "");

        public static void Unmount(string specialName) => Unmount(-1, specialName);
        private static void Unmount(int deviceId, string specialName) {

            if (deviceId == -1 && (specialName == null || specialName.Length == 0))
                throw new Exception("Could not found the device for unmount");

            var dir = specialName != "" ? specialName : "FS" + deviceId;

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
