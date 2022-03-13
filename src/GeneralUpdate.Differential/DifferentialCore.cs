﻿using GeneralUpdate.Common.Models;
using GeneralUpdate.Common.Utils;
using GeneralUpdate.Differential.Binary;
using GeneralUpdate.Zip;
using GeneralUpdate.Zip.Events;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.Differential
{
    public sealed class DifferentialCore
    {
        #region Private Members

        /// <summary>
        /// Differential file format .
        /// </summary>
        private const string DIFF_FORMAT = ".patch";
        private const string SEVENZ_FORMAT = ".7z";
        private const string ZIP_FORMAT = ".zip";
        private static readonly object _lockObj = new object();
        private static DifferentialCore _instance;

        private Action<object, BaseCompressProgressEventArgs> _compressProgressCallback;

        #endregion

        #region Constructors

        private DifferentialCore() { }

        #endregion

        #region Public Properties

        public static DifferentialCore Instance
        {
            get 
            {
                if (_instance == null) 
                {
                    lock (_lockObj)
                    {
                        if (_instance == null)
                        {
                            _instance = new DifferentialCore();
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Generate patch file [Cannot contain files with the same name but different extensions] .
        /// </summary>
        /// <param name="appPath">Previous version folder path .</param>
        /// <param name="targetPath">Recent version folder path.</param>
        /// <param name="patchPath">Store discovered incremental update files in a temporary directory .</param>
        /// <param name="compressProgressCallback">Incremental package generation progress callback function.</param>
        /// <param name="type">7z or zip</param>
        /// <param name="encoding">Incremental packet encoding format .</param>
        /// <returns></returns>
        public async Task Clean(string appPath, string targetPath , string patchPath = null, Action<object, BaseCompressProgressEventArgs> compressProgressCallback = null, OperationType type = OperationType.GZip, Encoding encoding = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(patchPath)) patchPath = Path.Combine(Environment.CurrentDirectory, "patchs");
                if(!Directory.Exists(patchPath)) Directory.CreateDirectory(patchPath);

                //Take the left tree as the center to match the files that are not in the right tree .
                var tupleResult = FileUtil.Compare(targetPath, appPath);

                //Binary differencing of like terms .
                foreach (var file in tupleResult.Item2)
                {
                    var oldfile = Path.Combine(appPath, file.Name);
                    var newfile = file.FullName;
                    if (File.Exists(oldfile) && File.Exists(newfile))
                    {
                        //Generate the difference file to the difference directory .
                        await new BinaryHandle().Clean(Path.Combine(appPath, file.Name), file.FullName,
                            Path.Combine(patchPath, $"{ Path.GetFileNameWithoutExtension(file.Name) }{ DIFF_FORMAT }"));
                    }
                    else
                    {
                        File.Copy(newfile, Path.Combine(patchPath, Path.GetFileName(newfile)),true);
                    }
                }
                _compressProgressCallback = compressProgressCallback;
                var factory = new GeneralZipFactory();
                if(_compressProgressCallback != null) factory.CompressProgress += OnCompressProgress;
                factory.CreatefOperate(type, patchPath, patchPath, false, encoding).CreatZip();
            }
            catch (Exception ex)
            {
                throw new Exception($"Generate error : { ex.Message } !", ex.InnerException);
            }
        }

        /// <summary>
        /// Apply patch [Cannot contain files with the same name but different extensions] .
        /// </summary>
        /// <param name="appPath">Client application directory .</param>
        /// <param name="patchPath"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task Drity(string appPath, string patchPath)
        {
            try
            {
                if(string.IsNullOrWhiteSpace(patchPath) || string.IsNullOrWhiteSpace(appPath))
                    throw new ArgumentNullException(nameof(appPath));

                var patchFiles = FileUtil.GetAllFiles(patchPath);
                var oldFiles = FileUtil.GetAllFiles(appPath);
                foreach (var oldFile in oldFiles)
                {
                    var findFile = patchFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f.Name).Equals(Path.GetFileNameWithoutExtension(oldFile.Name)));
                    if (findFile != null) await DrityPatch(oldFile.FullName, findFile.FullName);
                }
                DrityNew(appPath, patchPath);
                if (Directory.Exists(patchPath)) Directory.Delete(patchPath, true);
            }
            catch (Exception ex)
            {
                throw new Exception($"Drity error : { ex.Message } !", ex.InnerException);
            }
        }

        /// <summary>
        /// Apply patch file .
        /// </summary>
        /// <param name="appPath">Client application directory .</param>
        /// <param name="patchPath"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task DrityPatch(string appPath,string patchPath) 
        {
            try
            {
                if (!File.Exists(appPath) || !File.Exists(patchPath)) return;
                var newPath = Path.Combine(Path.GetDirectoryName(appPath), $"{ Path.GetRandomFileName() }_{ Path.GetFileName(appPath) }");
                await new BinaryHandle().Drity(appPath, newPath, patchPath);
                File.Delete(appPath);
                File.Move(newPath, appPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"RevertFile error : { ex.Message } !", ex.InnerException);
            }
        }

        /// <summary>
        /// Add new files .
        /// </summary>
        /// <param name="appPath">Client application directory .</param>
        /// <param name="patchPath"></param>
        private void DrityNew(string appPath, string patchPath) 
        {
            try
            {
                var listExcept = FileUtil.Compare(patchPath, appPath);
                foreach (var file in listExcept.Item2)
                {
                    var extensionName = Path.GetExtension(file.FullName);
                    if (extensionName.Equals(DIFF_FORMAT) || extensionName.Equals(SEVENZ_FORMAT) || extensionName.Equals(ZIP_FORMAT)) continue;
                    File.Copy(file.FullName, Path.Combine(appPath, file.Name), true);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($" DrityNew error : { ex.Message } !", ex.InnerException);
            }
        }

        #endregion

        #region Private Methods

        private void OnCompressProgress(object sender, BaseCompressProgressEventArgs e) => _compressProgressCallback(sender, e);

        #endregion
    }
}