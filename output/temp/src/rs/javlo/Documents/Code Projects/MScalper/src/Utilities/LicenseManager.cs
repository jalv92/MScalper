using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MScalper.Utilities
{
    /// <summary>
    /// Gestor de licencias para MScalper
    /// Implementa verificación de licencia basada en ID de máquina
    /// </summary>
    public class LicenseManager
    {
        #region Private Fields
        private const string CREATOR_MID = "C6CF79C74B4AA01E152615AB23C6C728";
        private const int TRIAL_DAYS = 14;
        private const string LICENSE_FILE_NAME = "msclic.dat";
        private const string ACTIVATION_URL = "https://api.mscalper.com/activate"; // URL para futura implementación de servidor
        
        private static readonly byte[] _encryptionKey = Encoding.UTF8.GetBytes("MS$C@1p3rS3cr3tK3y!2025"); // Clave de 24 bytes para TripleDES
        private static readonly byte[] _encryptionIV = Encoding.UTF8.GetBytes("I70mS8#2"); // IV de 8 bytes para TripleDES

        private static LicenseManager _instance;
        private readonly string _licenseFilePath;
        private LicenseInfo _currentLicense;
        private bool _isInitialized = false;
        private readonly object _lockObject = new object();
        #endregion

        #region License Info Class
        /// <summary>
        /// Información de licencia
        /// </summary>
        private class LicenseInfo
        {
            public string MachineId { get; set; }
            public string LicenseKey { get; set; }
            public string UserName { get; set; }
            public string Email { get; set; }
            public DateTime ActivationDate { get; set; }
            public DateTime ExpirationDate { get; set; }
            public LicenseType Type { get; set; }
            public List<string> Features { get; set; }
            public int ActivationCount { get; set; }
            public DateTime LastCheckDate { get; set; }
            public string ValidationHash { get; set; }

            public LicenseInfo()
            {
                Features = new List<string>();
            }
        }

        /// <summary>
        /// Tipos de licencia soportados
        /// </summary>
        public enum LicenseType
        {
            Trial,
            Creator,
            Basic,
            Professional,
            Enterprise
        }
        #endregion

        #region Singleton Instance
        /// <summary>
        /// Obtiene la instancia única de LicenseManager
        /// </summary>
        public static LicenseManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new LicenseManager();
                }
                return _instance;
            }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor privado para patrón singleton
        /// </summary>
        private LicenseManager()
        {
            // Determinar la ruta del archivo de licencia
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string licenseDir = Path.Combine(appDataPath, "MScalper");
            
            // Crear directorio si no existe
            if (!Directory.Exists(licenseDir))
            {
                Directory.CreateDirectory(licenseDir);
            }
            
            _licenseFilePath = Path.Combine(licenseDir, LICENSE_FILE_NAME);
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Inicializa y verifica la licencia
        /// </summary>
        /// <returns>True si la licencia es válida</returns>
        public bool Initialize()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_isInitialized)
                        return IsLicenseValid();

                    // Generar o cargar información de licencia
                    if (!LoadLicense())
                    {
                        // Si no se puede cargar, verificar si es el creador
                        string machineId = GetMachineId();
                        
                        if (machineId == CREATOR_MID)
                        {
                            // Es el creador, generar licencia especial
                            _currentLicense = CreateCreatorLicense(machineId);
                        }
                        else
                        {
                            // Nuevo usuario, crear licencia de prueba
                            _currentLicense = CreateTrialLicense(machineId);
                        }
                        
                        // Guardar nueva licencia
                        SaveLicense();
                    }

                    // Verificar validez
                    bool isValid = VerifyLicense();
                    
                    // Actualizar fecha de comprobación
                    _currentLicense.LastCheckDate = DateTime.Now;
                    SaveLicense();
                    
                    _isInitialized = true;
                    return isValid;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error inicializando licencia: {ex.Message}", Logger.LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// Verifica si la licencia actual es válida
        /// </summary>
        /// <returns>True si la licencia es válida</returns>
        public bool IsLicenseValid()
        {
            if (!_isInitialized)
                return false;
                
            lock (_lockObject)
            {
                try
                {
                    // Si ya han pasado más de 24 horas desde la última verificación, 
                    // volver a verificar completamente
                    if ((DateTime.Now - _currentLicense.LastCheckDate).TotalHours > 24)
                    {
                        bool isValid = VerifyLicense();
                        _currentLicense.LastCheckDate = DateTime.Now;
                        SaveLicense();
                        return isValid;
                    }
                    
                    // Verificación rápida para llamadas frecuentes
                    return _currentLicense != null && 
                           _currentLicense.ExpirationDate > DateTime.Now &&
                           VerifyLicenseHash();
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Obtiene la fecha de expiración de la licencia
        /// </summary>
        /// <returns>Fecha de expiración o DateTime.MinValue si no hay licencia</returns>
        public DateTime GetExpirationDate()
        {
            if (!_isInitialized || _currentLicense == null)
                return DateTime.MinValue;
                
            return _currentLicense.ExpirationDate;
        }

        /// <summary>
        /// Obtiene el tipo de licencia actual
        /// </summary>
        /// <returns>Tipo de licencia</returns>
        public LicenseType GetLicenseType()
        {
            if (!_isInitialized || _currentLicense == null)
                return LicenseType.Trial;
                
            return _currentLicense.Type;
        }

        /// <summary>
        /// Verifica si la licencia tiene una característica específica
        /// </summary>
        /// <param name="featureName">Nombre de la característica</param>
        /// <returns>True si la licencia incluye la característica</returns>
        public bool HasFeature(string featureName)
        {
            if (!_isInitialized || _currentLicense == null)
                return false;
                
            return _currentLicense.Features.Contains(featureName);
        }

        /// <summary>
        /// Obtiene los días restantes de licencia
        /// </summary>
        /// <returns>Número de días restantes, o -1 si es licencia permanente</returns>
        public int GetRemainingDays()
        {
            if (!_isInitialized || _currentLicense == null)
                return 0;
                
            if (_currentLicense.Type == LicenseType.Creator)
                return -1; // Licencia permanente
                
            return Math.Max(0, (int)(_currentLicense.ExpirationDate - DateTime.Now).TotalDays);
        }

        /// <summary>
        /// Activa la licencia con una clave proporcionada
        /// </summary>
        /// <param name="licenseKey">Clave de licencia</param>
        /// <param name="userName">Nombre de usuario</param>
        /// <param name="email">Email de usuario</param>
        /// <returns>True si la activación fue exitosa</returns>
        public async Task<bool> ActivateLicense(string licenseKey, string userName, string email)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
                return false;
                
            try
            {
                lock (_lockObject)
                {
                    // Aquí se implementaría la lógica de conexión con el servidor de activación
                    // Por ahora, simulamos una activación básica
                    
                    // Preparar datos de licencia
                    string machineId = GetMachineId();
                    _currentLicense = new LicenseInfo
                    {
                        MachineId = machineId,
                        LicenseKey = licenseKey,
                        UserName = userName,
                        Email = email,
                        ActivationDate = DateTime.Now,
                        ExpirationDate = DateTime.Now.AddYears(1), // Licencia por 1 año
                        Type = LicenseType.Basic,
                        ActivationCount = 1,
                        LastCheckDate = DateTime.Now,
                        Features = new List<string> { "BasicFeatures", "Indicators", "Trading" }
                    };
                    
                    // Generar hash de validación
                    _currentLicense.ValidationHash = GenerateLicenseHash(_currentLicense);
                    
                    // Guardar licencia
                    SaveLicense();
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error activando licencia: {ex.Message}", Logger.LogLevel.Error);
                return false;
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Carga la información de licencia desde el archivo
        /// </summary>
        /// <returns>True si se cargó correctamente</returns>
        private bool LoadLicense()
        {
            try
            {
                if (!File.Exists(_licenseFilePath))
                    return false;
                    
                // Leer y desencriptar datos
                byte[] encryptedData = File.ReadAllBytes(_licenseFilePath);
                string licenseJson = DecryptData(encryptedData);
                
                // Deserializar
                _currentLicense = JsonConvert.DeserializeObject<LicenseInfo>(licenseJson);
                
                return _currentLicense != null;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error cargando licencia: {ex.Message}", Logger.LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// Guarda la información de licencia en el archivo
        /// </summary>
        /// <returns>True si se guardó correctamente</returns>
        private bool SaveLicense()
        {
            try
            {
                if (_currentLicense == null)
                    return false;
                    
                // Serializar y encriptar
                string licenseJson = JsonConvert.SerializeObject(_currentLicense);
                byte[] encryptedData = EncryptData(licenseJson);
                
                // Guardar en archivo
                File.WriteAllBytes(_licenseFilePath, encryptedData);
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error guardando licencia: {ex.Message}", Logger.LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// Verifica la validez de la licencia actual
        /// </summary>
        /// <returns>True si la licencia es válida</returns>
        private bool VerifyLicense()
        {
            try
            {
                if (_currentLicense == null)
                    return false;
                    
                // Verificar ID de máquina
                string currentMachineId = GetMachineId();
                if (_currentLicense.MachineId != currentMachineId)
                    return false;
                    
                // Verificar fecha de expiración
                if (_currentLicense.ExpirationDate < DateTime.Now)
                    return false;
                    
                // Verificar hash de validación
                return VerifyLicenseHash();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifica el hash de validación de la licencia
        /// </summary>
        /// <returns>True si el hash es válido</returns>
        private bool VerifyLicenseHash()
        {
            try
            {
                if (_currentLicense == null)
                    return false;
                    
                string expectedHash = _currentLicense.ValidationHash;
                string actualHash = GenerateLicenseHash(_currentLicense);
                
                return expectedHash == actualHash;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Crea una licencia para el creador del proyecto
        /// </summary>
        /// <param name="machineId">ID de la máquina</param>
        /// <returns>Información de licencia</returns>
        private LicenseInfo CreateCreatorLicense(string machineId)
        {
            var license = new LicenseInfo
            {
                MachineId = machineId,
                LicenseKey = "CREATOR-" + Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper(),
                UserName = "Javier Lora",
                Email = "creator@mscalper.com",
                ActivationDate = DateTime.Now,
                ExpirationDate = DateTime.Now.AddYears(100), // Prácticamente permanente
                Type = LicenseType.Creator,
                ActivationCount = 0,
                LastCheckDate = DateTime.Now,
                Features = new List<string> { "AllFeatures", "Indicators", "Trading", "Advanced" }
            };
            
            // Generar hash de validación
            license.ValidationHash = GenerateLicenseHash(license);
            
            return license;
        }

        /// <summary>
        /// Crea una licencia de prueba por 14 días
        /// </summary>
        /// <param name="machineId">ID de la máquina</param>
        /// <returns>Información de licencia</returns>
        private LicenseInfo CreateTrialLicense(string machineId)
        {
            var license = new LicenseInfo
            {
                MachineId = machineId,
                LicenseKey = "TRIAL-" + Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper(),
                UserName = "Trial User",
                Email = "",
                ActivationDate = DateTime.Now,
                ExpirationDate = DateTime.Now.AddDays(TRIAL_DAYS),
                Type = LicenseType.Trial,
                ActivationCount = 0,
                LastCheckDate = DateTime.Now,
                Features = new List<string> { "BasicFeatures", "Indicators", "Trading" }
            };
            
            // Generar hash de validación
            license.ValidationHash = GenerateLicenseHash(license);
            
            return license;
        }

        /// <summary>
        /// Genera un hash para validar la integridad de la licencia
        /// </summary>
        /// <param name="license">Información de licencia</param>
        /// <returns>Hash calculado</returns>
        private string GenerateLicenseHash(LicenseInfo license)
        {
            if (license == null)
                return string.Empty;
                
            // Construir string para hash (excluir el propio hash)
            StringBuilder sb = new StringBuilder();
            sb.Append(license.MachineId);
            sb.Append(license.LicenseKey);
            sb.Append(license.UserName);
            sb.Append(license.Email);
            sb.Append(license.ActivationDate.ToString("yyyyMMddHHmmss"));
            sb.Append(license.ExpirationDate.ToString("yyyyMMddHHmmss"));
            sb.Append((int)license.Type);
            sb.Append(license.ActivationCount);
            sb.Append(string.Join(",", license.Features));
            
            // Calcular SHA256
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(sb.ToString());
                byte[] hashBytes = sha256.ComputeHash(inputBytes);
                
                // Convertir a string hexadecimal
                return BitConverter.ToString(hashBytes).Replace("-", "");
            }
        }

        /// <summary>
        /// Obtiene un ID único para la máquina actual
        /// </summary>
        /// <returns>ID de máquina</returns>
        private string GetMachineId()
        {
            // Para sobreescribir el ID de la máquina durante desarrollo/pruebas
            // Esto permitiría simular diferentes máquinas
            string overrideMid = Environment.GetEnvironmentVariable("OFS_MACHINE_ID_OVERRIDE");
            if (!string.IsNullOrEmpty(overrideMid))
                return overrideMid;
                
            try
            {
                // Recopilar información de hardware para generar un ID único
                StringBuilder sb = new StringBuilder();
                
                // CPU ID
                using (ManagementClass mc = new ManagementClass("Win32_Processor"))
                {
                    ManagementObjectCollection moc = mc.GetInstances();
                    foreach (ManagementObject mo in moc)
                    {
                        sb.Append(mo["ProcessorId"]?.ToString() ?? "");
                        break;
                    }
                }
                
                // Placa base
                using (ManagementClass mc = new ManagementClass("Win32_BaseBoard"))
                {
                    ManagementObjectCollection moc = mc.GetInstances();
                    foreach (ManagementObject mo in moc)
                    {
                        sb.Append(mo["SerialNumber"]?.ToString() ?? "");
                        break;
                    }
                }
                
                // MAC Address (primera interfaz física)
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        nic.OperationalStatus == OperationalStatus.Up)
                    {
                        sb.Append(BitConverter.ToString(nic.GetPhysicalAddress().GetAddressBytes()));
                        break;
                    }
                }
                
                // Volume Serial del disco C:
                using (ManagementObject disk = new ManagementObject(@"Win32_LogicalDisk.DeviceID='C:'"))
                {
                    disk.Get();
                    sb.Append(disk["VolumeSerialNumber"]?.ToString() ?? "");
                }
                
                // Calcular hash MD5 para obtener un identificador más corto
                using (MD5 md5 = MD5.Create())
                {
                    byte[] inputBytes = Encoding.UTF8.GetBytes(sb.ToString());
                    byte[] hashBytes = md5.ComputeHash(inputBytes);
                    
                    // Convertir a string hexadecimal
                    return BitConverter.ToString(hashBytes).Replace("-", "");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error obteniendo ID de máquina: {ex.Message}", Logger.LogLevel.Error);
                
                // Fallback: usar combinación de nombre de equipo y usuario actual
                string fallbackInput = Environment.MachineName + Environment.UserName + 
                                      Environment.ProcessorCount.ToString();
                                      
                using (MD5 md5 = MD5.Create())
                {
                    byte[] inputBytes = Encoding.UTF8.GetBytes(fallbackInput);
                    byte[] hashBytes = md5.ComputeHash(inputBytes);
                    return BitConverter.ToString(hashBytes).Replace("-", "");
                }
            }
        }

        /// <summary>
        /// Encripta datos con Triple DES
        /// </summary>
        /// <param name="plainText">Texto a encriptar</param>
        /// <returns>Datos encriptados</returns>
        private byte[] EncryptData(string plainText)
        {
            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                
                using (TripleDESCryptoServiceProvider tdes = new TripleDESCryptoServiceProvider())
                {
                    tdes.Key = _encryptionKey;
                    tdes.IV = _encryptionIV;
                    tdes.Mode = CipherMode.CBC;
                    tdes.Padding = PaddingMode.PKCS7;
                    
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, tdes.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(plainBytes, 0, plainBytes.Length);
                            cs.FlushFinalBlock();
                        }
                        return ms.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error encriptando datos: {ex.Message}", Logger.LogLevel.Error);
                throw;
            }
        }

        /// <summary>
        /// Desencripta datos con Triple DES
        /// </summary>
        /// <param name="encryptedData">Datos encriptados</param>
        /// <returns>Texto desencriptado</returns>
        private string DecryptData(byte[] encryptedData)
        {
            try
            {
                using (TripleDESCryptoServiceProvider tdes = new TripleDESCryptoServiceProvider())
                {
                    tdes.Key = _encryptionKey;
                    tdes.IV = _encryptionIV;
                    tdes.Mode = CipherMode.CBC;
                    tdes.Padding = PaddingMode.PKCS7;
                    
                    using (MemoryStream ms = new MemoryStream(encryptedData))
                    {
                        using (CryptoStream cs = new CryptoStream(ms, tdes.CreateDecryptor(), CryptoStreamMode.Read))
                        {
                            using (StreamReader sr = new StreamReader(cs, Encoding.UTF8))
                            {
                                return sr.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error desencriptando datos: {ex.Message}", Logger.LogLevel.Error);
                throw;
            }
        }
        #endregion
    }
}