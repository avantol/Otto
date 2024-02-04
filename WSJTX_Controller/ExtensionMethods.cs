using System.Linq;
using System.Text;

namespace WsjtxUdpLib
{
    internal static class ExtensionMethods
    {
        public static string ToCompactLine(this object o, params string[] argsToSkip)
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (var prop in o.GetType().GetProperties())
            {
                if (argsToSkip != null && argsToSkip.Contains(prop.Name))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(" ");
                    if ((++n % 6) == 0)
                    {
                        sb.Append("\n           ");
                    }
                }


                sb.Append(prop.Name.Substring(0, 1).ToLower());
                sb.Append(prop.Name.Substring(1));
                sb.Append(":");
                sb.Append(prop.GetValue(o));
            }

            return sb.ToString();
        }
    }
}
