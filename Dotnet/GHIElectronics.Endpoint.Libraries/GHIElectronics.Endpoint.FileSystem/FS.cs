using System.IO;
using GHIElectronics.Endpoint.Core;

namespace GHIElectronics.Endpoint {
    public static class FileSystem {

    /**<example>
    Write file to USB
    <code>
    using System.Text;
    using GHIElectronics.Endpoint.Devices.UsbHost;

    var usbhost = new UsbHostController();
    usbhost.OnConnectionChangedEvent += Usbhost_OnConnectionChangedEvent;

    </code>
    </example>*/

        const string ROOT_FS_FOLDER = "/media";
        public static string Mount(string deviceName) {
            Script script;
            string arg;

            if (deviceName == null || deviceName.Length == 0)
                throw new Exception("Invalid argument");

            var folder_name = Guid.NewGuid().ToString("d");

           

            var dir = ROOT_FS_FOLDER + "/" + folder_name;

            if (Directory.Exists(dir)) {
                throw new Exception("The directory is already exist. Use different mountedName");
            }

            if (!Directory.Exists(dir)) {
                arg = string.Format("{0}", dir);
                script = new Script("mkdir", "./", arg);

                script.Start();
            }

            if (Directory.Exists(dir)) {

                arg = string.Format("{0} {1}", deviceName, dir);

                script = new Script("mount", "./", arg);

                script.Start();

                if (script.ExitCode == 0)
                    return dir;

            }
            throw new Exception("Could not mount the device " + folder_name);

        }
        public static void Unmount(string path) {

            if (path == null || path.Length == 0)
                throw new Exception("Could not found the device for unmount");

            var dir = path;

            var script = new Script("umount", "./", dir);

            script.Start();

            if (script.ExitCode == 0) {

                var arg = string.Format("-r {0}", dir);

                script = new Script("rm", "./", arg);

                script.Start();

                if (script.ExitCode == 0) {
                    return;
                }
            }

            throw new Exception("Could not umount the device " + dir);


        }

        public static void Flush() {
            var script = new Script("sync", "./", "");

            script.Start();
        }
    }
}
