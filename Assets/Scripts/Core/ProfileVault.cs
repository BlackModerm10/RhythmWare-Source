using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace RhythmWare.Core
{
    public static class ProfileVault
    {
        [Serializable] public sealed class Secrets {public string refreshToken,recoveryCode;}
        [StructLayout(LayoutKind.Sequential)] struct Blob {public int size;public IntPtr data;}
        [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)] static extern bool CryptProtectData(ref Blob input,string description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
        [DllImport("crypt32.dll",SetLastError=true)] static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
        [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
        static byte[] Protect(byte[] bytes,bool decrypt)
        {
            if(Application.platform!=RuntimePlatform.WindowsPlayer&&Application.platform!=RuntimePlatform.WindowsEditor)throw new PlatformNotSupportedException("Esta beta guarda credenciales mediante la protección de Windows.");
            var input=new Blob{size=bytes.Length,data=Marshal.AllocHGlobal(bytes.Length)};Blob output=default;
            try {
                Marshal.Copy(bytes,0,input.data,bytes.Length);
                bool ok=decrypt?CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptProtectData(ref input,"RhythmWare",IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
                if(!ok)throw new CryptographicException("Windows no pudo proteger o abrir el acceso de este perfil.");
                var result=new byte[output.size];Marshal.Copy(output.data,result,0,result.Length);return result;
            } finally {Marshal.FreeHGlobal(input.data);if(output.data!=IntPtr.Zero)LocalFree(output.data);}
        }
        public static void Save(string id,Secrets secrets)=>ProfileStorage.AtomicWrite(Path.Combine(ProfileStorage.For(id),"access.bin"),Convert.ToBase64String(Protect(Encoding.UTF8.GetBytes(JsonUtility.ToJson(secrets)),false)));
        public static Secrets Load(string id)
        {
            string path=Path.Combine(ProfileStorage.For(id),"access.bin");
            if(!File.Exists(path))return new Secrets();
            try{return JsonUtility.FromJson<Secrets>(Encoding.UTF8.GetString(Protect(Convert.FromBase64String(File.ReadAllText(path)),true)))??new Secrets();}
            catch{throw new IOException("No se pudo abrir el acceso guardado. Usa tu código de recuperación; los resultados locales se conservan.");}
        }
        public static string NewRecoveryCode(){var bytes=new byte[32];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(bytes);return "RW-"+Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');}
        public static bool ValidRecoveryCode(string code)=>code!=null&&System.Text.RegularExpressions.Regex.IsMatch(code,@"^RW-[A-Za-z0-9_-]{43}$");
    }
}
