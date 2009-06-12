#define OPTIMIZE_WI6612

// ZipDirEntry.cs
// ------------------------------------------------------------------
//
// Copyright (c) 2006, 2007, 2008, 2009 Dino Chiesa and Microsoft Corporation.  
// All rights reserved.
//
// This code module is part of DotNetZip, a zipfile class library.
//
// ------------------------------------------------------------------
//
// This code is licensed under the Microsoft Public License. 
// See the file License.txt for the license details.
// More info on: http://dotnetzip.codeplex.com
//
// ------------------------------------------------------------------
//
// last saved (in emacs): 
// Time-stamp: <2009-May-29 17:37:01>
//
// ------------------------------------------------------------------
//
// This module defines members of the ZipEntry class for reading the
// Zip file central directory.
//
// Created: Tue, 27 Mar 2007  15:30
// 
// ------------------------------------------------------------------


using System;

namespace Ionic.Zip
{

    partial class ZipEntry
    {
        /// <summary>
        /// True if the referenced entry is a directory.  
        /// </summary>
        internal bool AttributesIndicateDirectory
        {
            get { return ((_InternalFileAttrs == 0) && ((_ExternalFileAttrs & 0x0010) == 0x0010)); }
        }


#if OPTIMIZE_WI6612
        internal void ResetDirEntry()
        {
            // The length of the "local header" for an entry is not necessarily
            // the same as the length of the record in the central directory.
            // Therefore we cannot know the __FileDataPosition until we read the
            // local header.

            //e._LengthOfHeader = 30 + _filenameLength + _extraFieldLength;

            //e.__FileDataPosition = e._RelativeOffsetOfHeader + 30 + _filenameLength + _extraFieldLength;
            // mark as -1 to indicate we need to read this later
            this.__FileDataPosition = -1;

            // The length of the "local header" for an entry is not necessarily the same as
            // the length of the record in the central directory.  
            //e._LengthOfHeader = 30 + _filenameLength + _extraFieldLength;
            this._LengthOfHeader = 0;  // mark as zero to indicate we need to read later
        }
#endif

        /// <summary>
        /// Reads one entry from the zip directory structure in the zip file. 
        /// </summary>
        /// <param name="s">the stream from which to read.</param>
        /// <param name="expectedEncoding">
        /// The text encoding to use if the entry is not marked UTF-8.
        /// </param>
        /// <returns>the entry read from the archive.</returns>
        internal static ZipEntry ReadDirEntry(System.IO.Stream s, System.Text.Encoding expectedEncoding)
        {
            long cdrPosition = s.Position;
            int signature = Ionic.Zip.SharedUtilities.ReadSignature(s);
            // return null if this is not a local file header signature
            if (IsNotValidZipDirEntrySig(signature))
            {
                s.Seek(-4, System.IO.SeekOrigin.Current);

                // Getting "not a ZipDirEntry signature" here is not always wrong or an error. 
                // This can happen when walking through a zipfile.  After the last ZipDirEntry, 
                // we expect to read an EndOfCentralDirectorySignature.  When we get this is how we 
                // know we've reached the end of the central directory. 
                if (signature != ZipConstants.EndOfCentralDirectorySignature &&
                    signature != ZipConstants.Zip64EndOfCentralDirectoryRecordSignature)
                {
                    throw new BadReadException(String.Format("  ZipEntry::ReadDirEntry(): Bad signature (0x{0:X8}) at position 0x{1:X8}", signature, s.Position));
                }
                return null;
            }

            int bytesRead = 42 + 4;
            byte[] block = new byte[42];
            int n = s.Read(block, 0, block.Length);
            if (n != block.Length) return null;

            int i = 0;
            ZipEntry zde = new ZipEntry();
            zde._Source = ZipEntrySource.Zipfile;
            zde._archiveStream = s;
            zde._cdrPosition = cdrPosition;

            unchecked
            {
                zde._VersionMadeBy = (short)(block[i++] + block[i++] * 256);
                zde._VersionNeeded = (short)(block[i++] + block[i++] * 256);
                zde._BitField = (short)(block[i++] + block[i++] * 256);
                zde._CompressionMethod = (short)(block[i++] + block[i++] * 256);
                zde._TimeBlob = block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256;
                zde._LastModified = Ionic.Zip.SharedUtilities.PackedToDateTime(zde._TimeBlob);
                zde._Crc32 = block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256;
                zde._CompressedSize = (uint)(block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256);
                zde._UncompressedSize = (uint)(block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256);
            }


            zde._filenameLength = (short)(block[i++] + block[i++] * 256);
            zde._extraFieldLength = (short)(block[i++] + block[i++] * 256);
            zde._commentLength = (short)(block[i++] + block[i++] * 256);
            //Int16 diskNumber = (short)(block[i++] + block[i++] * 256);
            i += 2;

            zde._InternalFileAttrs = (short)(block[i++] + block[i++] * 256);
            zde._ExternalFileAttrs = block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256;

            zde._RelativeOffsetOfLocalHeader = (uint)(block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256);

            // workitem 7801
            zde.IsText = ((zde._InternalFileAttrs & 0x01) == 0x01);

            block = new byte[zde._filenameLength];
            n = s.Read(block, 0, block.Length);
            bytesRead += n;
            if ((zde._BitField & 0x0800) == 0x0800)
            {
                // UTF-8 is in use
                zde._LocalFileName = Ionic.Zip.SharedUtilities.Utf8StringFromBuffer(block, block.Length);
            }
            else
            {
                zde._LocalFileName = Ionic.Zip.SharedUtilities.StringFromBuffer(block, block.Length, expectedEncoding);
            }


            // Console.WriteLine("\nEntry : {0}", zde._LocalFileName);
            // Console.WriteLine("  V Madeby/Needed:      0x{0:X4} / 0x{1:X4}", zde._VersionMadeBy, zde._VersionNeeded);
            // Console.WriteLine("  BitField/Compression: 0x{0:X4} / 0x{1:X4}", zde._BitField, zde._CompressionMethod);
            // Console.WriteLine("  Lastmod:              {0}", zde._LastModified.ToString("u"));
            // Console.WriteLine("  CRC:                  0x{0:X8}", zde._Crc32);
            // Console.WriteLine("  Comp / Uncomp:        0x{0:X8} ({0})   0x{1:X8} ({1})", zde._CompressedSize, zde._UncompressedSize);
            
            zde._FileNameInArchive = zde._LocalFileName;

            if (zde.AttributesIndicateDirectory) zde.MarkAsDirectory();  // may append a slash to filename if nec.

            // workitem 6898
            if (zde._LocalFileName.EndsWith("/")) zde.MarkAsDirectory();


            zde._CompressedFileDataSize = zde._CompressedSize;
            if ((zde._BitField & 0x01) == 0x01)
            {
                zde._Encryption = EncryptionAlgorithm.PkzipWeak; // this may change after processing the Extra field
                zde._sourceIsEncrypted = true;
            }

            if (zde._extraFieldLength > 0)
            {
                // Console.WriteLine("  ZDE Extra Field:      {0} bytes", zde._extraFieldLength);
                zde._InputUsesZip64 = (zde._CompressedSize == 0xFFFFFFFF ||
                      zde._UncompressedSize == 0xFFFFFFFF ||
                      zde._RelativeOffsetOfLocalHeader == 0xFFFFFFFF);

                // Console.WriteLine("  Input uses Z64?:      {0}", zde._InputUsesZip64);

                bytesRead += zde.ProcessExtraField(zde._extraFieldLength);
                zde._CompressedFileDataSize = zde._CompressedSize;

                // if (zde._InputUsesZip64)
                // {
                // Console.WriteLine("  Z64 updated values");
                // Console.WriteLine("    Comp / Uncomp:      0x{0:X16} ({0})   0x{1:X16} ({1})", zde._CompressedSize, zde._UncompressedSize);
                // }
            }

            // we've processed the extra field, so we know the encryption method is set now.
            if (zde._Encryption == EncryptionAlgorithm.PkzipWeak)
            {
                // the "encryption header" of 12 bytes precedes the file data
                zde._CompressedFileDataSize -= 12;
            }
#if AESCRYPTO
            else if (zde.Encryption == EncryptionAlgorithm.WinZipAes128 ||
                        zde.Encryption == EncryptionAlgorithm.WinZipAes256)
            {
                zde._CompressedFileDataSize = zde.CompressedSize -
                    ((zde._KeyStrengthInBits / 8 / 2) + 10 + 2);// zde._aesCrypto.SizeOfEncryptionMetadata;
                zde._LengthOfTrailer = 10;
                //Console.WriteLine("  CFDS:        0x{0:X8} ({0})", zde._CompressedFileDataSize);
                //Console.WriteLine("  Actual Compression: 0x{0:X4}", zde._CompressionMethod);
            }
#endif
            if (zde._commentLength > 0)
            {
                block = new byte[zde._commentLength];
                n = s.Read(block, 0, block.Length);
                bytesRead += n;
                if ((zde._BitField & 0x0800) == 0x0800)
                {
                    // UTF-8 is in use
                    zde._Comment = Ionic.Zip.SharedUtilities.Utf8StringFromBuffer(block, block.Length);
                }
                else
                {
                    zde._Comment = Ionic.Zip.SharedUtilities.StringFromBuffer(block, block.Length, expectedEncoding);
                }
            }
            zde._LengthOfDirEntry = bytesRead;
            return zde;
        }

        /// <summary>
        /// Returns true if the passed-in value is a valid signature for a ZipDirEntry. 
        /// </summary>
        /// <param name="signature">the candidate 4-byte signature value.</param>
        /// <returns>true, if the signature is valid according to the PKWare spec.</returns>
        internal static bool IsNotValidZipDirEntrySig(int signature)
        {
            return (signature != ZipConstants.ZipDirEntrySignature);
        }


        private Int16 _VersionMadeBy;
        private Int16 _InternalFileAttrs;
        private Int32 _ExternalFileAttrs;

        private Int32 _LengthOfDirEntry;
        private Int16 _filenameLength;
        private Int16 _extraFieldLength;
        private Int16 _commentLength;
    }


}