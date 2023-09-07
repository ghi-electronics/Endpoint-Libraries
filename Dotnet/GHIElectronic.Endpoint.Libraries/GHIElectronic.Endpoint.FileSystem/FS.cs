using GHIElectronic.Endpoint.Core;

namespace GHIElectronic.Endpoint {
    public static class FileSystem {
        public static string Mount(int device_id, string bus_id = "/dev/sda1") {
            var folder_name = "FS" + device_id;

            var arg = string.Format("{0}", folder_name);
            var script = new Script("mkdir", "/root", arg);

            var dir = "/root/" + folder_name;

            if (!Directory.Exists(dir)) {
                script.Start();
            }

            if (Directory.Exists(dir)) {

                arg = string.Format("{0} {1}", bus_id, folder_name);

                script = new Script("mount", "/root", arg);

                script.Start();

                if (script.ExitCode == 0)
                    return dir;

            }
            throw new Exception("Could not mount the device " + folder_name);

        }

        public static void Unmount(int device_id ) {
            var folder_name = "FS" + device_id;

            var dir = "/root/" + folder_name;

            var arg = string.Format("-r {0}", dir);

            var script = new Script("umount", "/root", dir);

            script.Start();

            if (script.ExitCode == 0) {
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
