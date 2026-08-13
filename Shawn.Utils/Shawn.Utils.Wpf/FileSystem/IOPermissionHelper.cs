using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Shawn.Utils.Wpf.FileSystem
{
    public static class IoPermissionHelper
    {
        public static bool HasWritePermissionOnDir(string path)
        {
            var writeAllow = false;
            var writeDeny = false;
            var di = new DirectoryInfo(path);
            while (di.Exists == false && di.Parent != null)
            {
                di = di.Parent;
            }
            if (di.Exists == false)
                return false;
            var accessControlList = di.GetAccessControl();
            var accessRules = accessControlList?.GetAccessRules(true, true,
                typeof(System.Security.Principal.SecurityIdentifier));
            if (accessRules == null)
                return false;

            foreach (FileSystemAccessRule rule in accessRules)
            {
                if ((FileSystemRights.Write & rule.FileSystemRights) != FileSystemRights.Write)
                    continue;
                if (rule.AccessControlType == AccessControlType.Allow)
                    writeAllow = true;
                else if (rule.AccessControlType == AccessControlType.Deny)
                    writeDeny = true;
            }

            return writeAllow && !writeDeny;
        }

        public static bool IsFileInUse(string fileName)
        {
            if (!File.Exists(fileName))
                return false;

            FileStream? fs = null;
            try
            {
                fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.None);
                return false;
            }
            catch
            {
                // ignored
            }
            finally
            {
                fs?.Close();
            }
            return true;
        }

        public static bool IsFileCanWriteNow(string fileName)
        {
            if (File.Exists(fileName) == false)
            {
                try
                {
                    File.WriteAllText(fileName, "");
                    return true;
                }
                catch
                {
                    // ignored
                }
                finally
                {
                    if (File.Exists(fileName))
                        File.Delete(fileName);
                }
                return false;
            }

            FileStream? fs = null;
            try
            {
                fs = new FileStream(fileName, FileMode.Open, FileAccess.Write, FileShare.None);
                return true;
            }
            catch
            {
                // ignored
            }
            finally
            {
                fs?.Close();
            }
            return false;
        }

        public static bool HasWritePermissionOnFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;
            var fi = new FileInfo(filePath);
            try
            {
                FileSystemSecurity security;
#if NETCOREAPP
                // nuget import System.IO.FileSystem.AccessControl
                if (File.Exists(filePath))
                {
                    security = new FileSecurity(filePath, AccessControlSections.Owner |
                                                          AccessControlSections.Group |
                                                          AccessControlSections.Access);
                }
                else
                {
                    if (fi.Directory != null)
                        return HasWritePermissionOnDir(fi.Directory.FullName);
                    else
                        return false;
                }
#else
                if (fi.Exists)
                {
                    security = File.GetAccessControl(filePath);
                }
                else if(fi?.Directory != null)
                {
                    return HasWritePermissionOnDir(fi.Directory.FullName);
                }
                else
                {
                    return false;
                }
#endif

                if (System.IO.File.GetAttributes(fi.FullName).ToString().IndexOf("ReadOnly") != -1)
                {
                    return false;
                }
                var rules = security.GetAccessRules(true, true, typeof(NTAccount));
                var currentUser = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                bool result = false;
                foreach (FileSystemAccessRule rule in rules)
                {
                    if (0 == (rule.FileSystemRights &
                              (FileSystemRights.WriteData | FileSystemRights.Write)))
                    {
                        continue;
                    }

                    if (rule.IdentityReference.Value.StartsWith("S-1-"))
                    {
                        var sid = new SecurityIdentifier(rule.IdentityReference.Value);
                        if (!currentUser.IsInRole(sid))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (!currentUser.IsInRole(rule.IdentityReference.Value))
                        {
                            continue;
                        }
                    }

                    if (rule.AccessControlType == AccessControlType.Deny)
                        return false;
                    if (rule.AccessControlType == AccessControlType.Allow)
                        result = true;
                }

                if (result)
                {
                    result = IsFileInUse(filePath) == false;
                }
                return result;
            }
            catch
            {
                return false;
            }
        }
    }
}