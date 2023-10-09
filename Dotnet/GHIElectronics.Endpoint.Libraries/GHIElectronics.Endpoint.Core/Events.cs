using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronics.Endpoint.Core {
    public static class Events {

        //static int start_count = 0;

        //public delegate void SystemEventChangedHandler(string eventlog);

        //static private SystemEventChangedHandler systemEventChangedCallbacks;

        //static public event SystemEventChangedHandler SystemEventChanged {
        //    add => systemEventChangedCallbacks += value;
        //    remove {
        //        if (systemEventChangedCallbacks != null) {
        //            systemEventChangedCallbacks -= value;
        //        }
        //    }
        //}

        //public static Task StartSystemEventDetectionTask() {
        //    start_count++;
        //    if (start_count == 1) {

        //        return Task.Run(() => {

        //            while (start_count > 0) {

        //                var script = new Script("dmesg", "/sbin/", "-c");

        //                script.Start();

        //                if (script.Output != "") {

        //                    var log = script.Output;
        //                    systemEventChangedCallbacks?.Invoke(log);
        //                }

        //                Thread.Sleep(2000);
        //            }
        //        }); ;
        //    }
        //    else {
        //        return Task.FromResult(0);  
        //    }
        //}

        //public static void StopSystemEventDetectionTask() => start_count--;
    }
}
