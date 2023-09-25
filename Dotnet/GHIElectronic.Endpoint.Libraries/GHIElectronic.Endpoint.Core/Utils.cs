using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronic.Endpoint.Core {
    static public class Utils {
        public static string GetUntil(this string that, char @char) {
            return that[..(IndexOf() == -1 ? that.Length : IndexOf())];
            int IndexOf() => that.IndexOf(@char);
        }

        public static string FindAndSplitUntil(string str, string substr, char until) {
            var pos_start = str.IndexOf(substr);

            if (pos_start == -1) {
                return string.Empty;
            }

            var pos_end = pos_start + substr.Length;

            while (pos_end < str.Length) {
                if (str[pos_end] == until) {
                    break;
                }

                pos_end++;
            }

            return str.Substring(pos_start, pos_end - pos_start);

        }
    }
}
