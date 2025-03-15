using System.Text;

namespace FindOdin {
    public class FindOdin {
        public static async Task<bool> OdinFound() {
            if (await Task.Run(() => File.Exists("/bin/odin4")))
            {
                return true;
            }
            return false;
        }
    }
}