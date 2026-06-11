using Apps2Samsung.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;

namespace Apps2Samsung.Helpers.Tizen.Certificate
{
    public class CertificateHelper
    {
        public List<ExistingCertificates> GetAvailableCertificates(params string[] certificateFolders)
        {
            var certificates = new List<ExistingCertificates>();
            var cipherUtil = new CipherUtil();
            List<string> duids = new List<string>();

            // Default item
            certificates.Add(new ExistingCertificates
            {
                Name = "Jelly2Sams (default)",
                Duid = string.Empty,
                File = null,
                ExpireDate = null
            });

            // Scan every supplied root: the per-user generated certs and the shipped bundled default.
            var p12Files = new List<string>();
            foreach (var folder in certificateFolders)
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                    continue;
                p12Files.AddRange(Directory.GetFiles(folder, "author.p12", SearchOption.AllDirectories));
            }

            foreach (var p12Path in p12Files)
            {
                var directory = Path.GetDirectoryName(p12Path);
                if (directory == null)
                    continue;

                var passwordPath = Path.Combine(directory, "password.txt");
                if (!File.Exists(passwordPath))
                    continue;

                var password = File.ReadAllText(passwordPath).Trim();
                if (string.IsNullOrWhiteSpace(password))
                    continue;
                try
                {
                    var cert = new X509Certificate2(
                        p12Path,
                        password,
                        X509KeyStorageFlags.Exportable);

                    var dist = new X509Certificate2(
                        p12Path.Replace("author.p12", "distributor.p12"),
                        password,
                        X509KeyStorageFlags.Exportable);

                    // Extract DUID
                    string duid = "";
                    foreach (var ext in dist.Extensions)
                    {
                        if (ext.Oid?.Value == "2.5.29.17") // Subject Alternative Name
                        {
                            try
                            {
                                var asnData = new AsnEncodedData(ext.Oid, ext.RawData);
                                var formatted = asnData.Format(true);

                                var lines = formatted.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                                foreach (var line in lines)
                                {
                                    if (line.Contains("URL=URN:tizen:deviceid=", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var parts = line.Split('=');
                                        if (parts.Length == 3)
                                        {
                                            duid = parts[2].Trim();
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    if (cert.NotAfter.Date >= DateTime.Today)
                    {
                        var name = cert.GetNameInfo(X509NameType.SimpleName, forIssuer: false);

                        // The same cert can appear in more than one scanned root (e.g. a copy
                        // migrated to the user dir while an old copy still sits in the bundle).
                        // Keep the first occurrence (user dir is scanned first).
                        bool alreadyAdded = certificates.Exists(c => c.Name == name && c.Duid == duid);
                        if (alreadyAdded)
                            continue;

                        certificates.Add(new ExistingCertificates
                        {
                            Name = name,
                            File = p12Path,
                            ExpireDate = cert.NotAfter,
                            Duid = duid
                        });
                    }

                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Failed to load certificate '{p12Path}': {ex}");
                }
            }

            return certificates;
        }
        public async Task HandleErrorResponse(HttpResponseMessage response)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                throw new Exception("You've made too many requests in a given amount of time.\nPlease wait and try your request again later.");
            }

            try
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(errorContent))
                {
                    var errorJson = JsonSerializer.Deserialize<JsonElement>(errorContent);
                    if (errorJson.TryGetProperty("error", out var errorObj))
                    {
                        var name = errorObj.TryGetProperty("name", out var nameEl) ? nameEl.ToString() : "";
                        var status = errorObj.TryGetProperty("status", out var statusEl) ? statusEl.ToString() : "";
                        var code = errorObj.TryGetProperty("code", out var codeEl) ? codeEl.ToString() : "";
                        var description = errorObj.TryGetProperty("description", out var descEl) ? descEl.ToString() : "";

                        throw new Exception($"Samsung API Error - Name: {name}, Status: {status}, Code: {code}, Description: {description}");
                    }
                }
            }
            catch (JsonException)
            {
            }

            throw new Exception($"Server response code: {response.StatusCode}");
        }
    }
}
