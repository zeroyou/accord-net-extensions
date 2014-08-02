#region Licence and Terms
// Accord.NET Extensions Framework
// https://github.com/dajuric/accord-net-extensions
//
// Copyright © Darko Jurić, 2014 
// darko.juric2@gmail.com
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU Lesser General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU Lesser General Public License for more details.
// 
//   You should have received a copy of the GNU Lesser General Public License
//   along with this program.  If not, see <https://www.gnu.org/licenses/lgpl.txt>.
//
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Accord.Extensions.Imaging;

namespace Accord.Extensions.Vision
{
    /// <summary>
    /// Represents directory stream-able source and provides functions and properties to access data in a stream-able way.
    /// </summary>
    public class ImageDirectoryReader : ImageStreamReader
    {
        string[] fileNames = null;

        Func<string, IImage> loader;
        long currentFrame = 0;

        #region Initialization

        /// <summary>
        /// Creates an instance of <see cref="ImageDirectoryReader"/>.
        /// </summary>
        /// <param name="dirPath">The directory path.</param>
        /// <param name="searchPattern">The image search pattern.</param>
        /// <param name="useNaturalSorting">Use natural sorting, otherwise raw image order is used.</param>
        /// <param name="recursive">If true searches the current directory and all subdirectories. Otherwise, only top directory is searched.</param>
        /// <param name="loader">Loader image function. If null default loader is used.</param>
        /// <exception cref="DirectoryNotFoundException">Directory can not be found.</exception>
        public ImageDirectoryReader(string dirPath, string searchPattern, bool useNaturalSorting = true, bool recursive = false, Func<string, IImage> loader = null)
            : this(dirPath, new string[] { searchPattern }, useNaturalSorting, recursive, loader)
        { }

         /// <summary>
        /// Creates an instance of <see cref="ImageDirectoryReader"/>.
        /// </summary>
        /// <param name="dirPath">The directory path.</param>
        /// <param name="searchPatterns">The image search patterns.</param>
        /// <param name="useNaturalSorting">Use natural sorting, otherwise raw image order is used.</param>
        /// <param name="recursive">If true searches the current directory and all subdirectories. Otherwise, only top directory is searched.</param>
        /// <param name="loader">Loader image function. If null default loader is used.</param>
        /// <exception cref="DirectoryNotFoundException">Directory can not be found.</exception>
        public ImageDirectoryReader(string dirPath, string[] searchPatterns, bool useNaturalSorting = true, bool recursive = false, Func<string, IImage> loader = null)
        {
            if (Directory.Exists(dirPath) == false)
                throw new DirectoryNotFoundException(String.Format("Dir: {0} cannot be found!", dirPath));

            loader = loader ?? cvLoader;

            this.IsLiveStream = false;
            this.CanSeek = true;

            this.loader = loader;

            DirectoryInfo directoryInfo = new DirectoryInfo(dirPath);
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            IEnumerable<string> files = null;

            if (useNaturalSorting)
            {
                files = directoryInfo.EnumerateFiles(searchPatterns, searchOption)
                        .OrderBy(f => f.FullName, new NaturalSortComparer()) //in case of problems replace f.FullName with f.Name
                        .Select(f => f.FullName);
            }
            else
            {
                files = from file in directoryInfo.EnumerateFiles(searchPatterns, searchOption)
                        select file.FullName;
            }

            this.fileNames = files.ToArray();
        }

        private static IImage cvLoader(string fileName)
        {
            var cvImg = CvHighGuiInvoke.cvLoadImage(fileName, ImageLoadType.Unchanged);
            var image = IplImage.FromPointer(cvImg).AsImage((_) => 
                                                      {
                                                          if (cvImg == IntPtr.Zero) return;
                                                          CvHighGuiInvoke.cvReleaseImage(ref cvImg);
                                                      });

            return image;
        }

        #endregion

        /// <summary>
        /// Open the current stream. This overload does not do anything.
        /// </summary>
        public override void Open() { }

        /// <summary>
        /// Closes the current stream. This overload resets position to zero.
        /// </summary>
        public override void Close()
        {
            currentFrame = 0;
        }

        object syncObj = new object();
        /// <summary>
        /// Reads an image from the stream.
        /// </summary>
        /// <param name="image">Read image.</param>
        /// <returns>True if the reading operation was successful, false otherwise.</returns>
        protected override bool ReadInternal(out IImage image)
        {
            lock (syncObj)
            {
                image = default(IImage);

                if (this.Position >= this.Length)
                    return false;

                image = loader(fileNames[currentFrame]);
                currentFrame++;
            }

            return true;
        }

        /// <summary>
        /// Gets the total number of files in the specified directory which match the specified search criteria.
        /// </summary>
        public override long Length { get { return fileNames.Length; } }

        /// <summary>
        /// Gets the current position within the stream.
        /// </summary>
        public override long Position { get { return currentFrame; } }

        /// <summary>
        /// Sets the position within the current stream.
        /// </summary>
        /// <param name="offset">A frame index offset relative to the origin parameter.</param>
        /// <param name="origin">A value of type <see cref="System.IO.SeekOrigin"/> indicating the reference point used to obtain the new position.</param>
        /// <returns>The new position within the current stream.</returns>
        public override long Seek(long offset, SeekOrigin origin = SeekOrigin.Current)
        {
            this.currentFrame = base.Seek(offset, origin);
            return Math.Max(0, Math.Min(currentFrame, this.Length));
        }


        #region Specific function

        /// <summary>
        /// Gets the current image file name.
        /// <para>If the position of the stream is equal to the stream length null is returned.</para>
        /// </summary>
        public string CurrentImageName
        {
            get { return (this.Position < fileNames.Length) ? fileNames[this.Position] : null; }
        }

        #endregion
    }
}
