namespace FindADBTools {
    public class FindAdbTools {
        public static async Task<bool> ADBFound() {
            if (await Task.Run(() => File.Exists("/bin/adb")))
            {
                return true;
            }
            return false;
        }
        public static async Task<bool> FastbootFound() {
            if (await Task.Run(() => File.Exists("/bin/fastboot")))
            {
                return true;
            }
            return false;
        }
    }
}