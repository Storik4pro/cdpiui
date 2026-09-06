using CDPIUI.Shared.Exceptions;
using CDPIUI.Shared.Exceptions.Catalog;
using CDPIUI.Shared.Exceptions.Database;
using CDPIUI.Shared.Exceptions.Interface;
using CDPIUI.Shared.Logger;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace CDPIUI.Shared.PrettyErrorConvertionService
{
    /// <summary>
    /// Gets pretty errors from exceptions to show in userspace
    /// </summary>
    public partial class ErrorConvertor
    {
        /// <summary>
        /// Get error code from exception
        /// </summary>
        /// <param name="ex">Exception</param>
        /// <param name="rawHResult">Error code in 0x00... format</param>
        /// <returns>Friendly error code</returns>
        public virtual PrettyErrorCode MapExceptionToCode(Exception ex, out uint? rawHResult)
        {
            return MapExceptionToCode(ex, out rawHResult, out _);
        }

        /// <summary>
        /// Get error code from exception
        /// </summary>
        /// <param name="ex">Exception</param>
        /// <param name="rawHResult">Error code in 0x00... format</param>
        /// <param name="statusCode">HTTP status code (if exsist)</param>
        /// <returns>Friendly error code</returns>
        public virtual PrettyErrorCode MapExceptionToCode(Exception ex, out uint? rawHResult, out int? statusCode)
        {
            statusCode = null;
            rawHResult = null;
            for (Exception current = ex; current != null; current = current.InnerException)
            {
                switch (current)
                {
                    case AddonNotInstalledException:
                        return PrettyErrorCode.ADDON_NOT_INSTALLED;
                    case MsiInstallException:
                        return PrettyErrorCode.MSI_INSTALL_FAILURE;
                    case UriFormatException:
                        return PrettyErrorCode.INVALID_URI;
                    
                    case FileNotFoundException:
                        return PrettyErrorCode.IO_FILE_NOT_FOUND;
                    case DirectoryNotFoundException:
                        return PrettyErrorCode.IO_DIRECTORY_NOT_FOUND;
                    case PathTooLongException:
                        return PrettyErrorCode.IO_PATH_TOO_LONG;
                    case UnauthorizedAccessException:
                        return PrettyErrorCode.IO_ACCESS_DENIED;
                    case CatalogNoSignature:
                        return PrettyErrorCode.CATALOG_SIGNATURE_CHECK_FAILURE;
                    case CatalogInvalid:
                        return PrettyErrorCode.CATALOG_INVALID;
                    case NewestVersionAlreadyInstalledException:
                        return PrettyErrorCode.NEWEST_VERSION_INSTALLED;
                    case UnknownFileFormatException:
                        return PrettyErrorCode.PACK_NOT_SUPPORTED;
                    case ApplicationFilesDamagedException:
                        return PrettyErrorCode.APPLICATION_DAMAGED_NEED_REPAIR;
                    case ICustomException:
                        return ((ICustomException)current).PrettyErrorCode;

                    case UnknownException:
                        return PrettyErrorCode.UNKNOWN;
                }
                if (current is IOException)
                {
                    var hrGeneric = unchecked((uint)current.HResult);

                    PrettyErrorCode code = ConvertHresultToCode(current.HResult, out rawHResult);

                    if (code != PrettyErrorCode.UNKNOWN) return code;

                    rawHResult = hrGeneric;
                    return PrettyErrorCode.IO_GENERIC;
                }
                if (current is InvalidDataException)
                    return PrettyErrorCode.EXTRACT_INVALID_ARCHIVE;

                int _hr = current.HResult;
                if (ex is Win32Exception w32ex)
                {
                    _hr = w32ex.NativeErrorCode;
                }

                if (_hr != 0)
                {
                    uint hr = unchecked((uint)_hr);
                    rawHResult = hr;
                    PrettyErrorCode code = ConvertHresultToCode(_hr, out rawHResult);

                    if (code != PrettyErrorCode.UNKNOWN) return code;
                }
            }
            if (ex is UriFormatException)
                return PrettyErrorCode.INVALID_URI;
            if (ex is TimeoutException)
                return PrettyErrorCode.TIMEOUT;
            return PrettyErrorCode.UNKNOWN;
        }

        private static PrettyErrorCode ConvertHresultToCode(int HResult, out uint? rawHResult)
        {
            uint hr = unchecked((uint)HResult);
            rawHResult = hr;
            switch (hr)
            {
                case 0:
                    return PrettyErrorCode.SUCCESS;
                case 0x80072EE7u:
                    return PrettyErrorCode.HOST_NAME_NOT_RESOLVED;
                case 0x80072EFDu:
                    return PrettyErrorCode.CANNOT_CONNECT;
                case 0x80072EFEu:
                    return PrettyErrorCode.CONNECTION_ABORTED;
                case 0x80072EE2u:
                    return PrettyErrorCode.TIMEOUT;
                case 0x80072F76u:
                    return PrettyErrorCode.ERROR_HTTP_HEADER_NOT_FOUND;
                case 0x80072F78u:
                    return PrettyErrorCode.ERROR_HTTP_INVALID_SERVER_RESPONSE;
                case 0x80072F8Fu:
                    return PrettyErrorCode.CERTIFICATE_EXPIRED;
                case 0x80072F8Eu:
                    return PrettyErrorCode.CERTIFICATE_COMMON_NAME_INCORRECT;
                case 0x80004005u:
                    break;
                case 0x80070005u:
                    return PrettyErrorCode.ACCESS_DENIED;
                case 0x80004004u:
                    return PrettyErrorCode.OPERATION_CANCELED;
                case 0x80070070u:
                    return PrettyErrorCode.IO_DISK_FULL;
                case 0x80070002:
                    return PrettyErrorCode.IO_FILE_NOT_FOUND;
                case 0x80070020:
                    return PrettyErrorCode.IO_FILE_USED_FOR_ANOTHER_PROCESS;
                case 0x000003EE:
                    return PrettyErrorCode.IO_FILE_INVALID;
                case 0x00000570:
                    return PrettyErrorCode.IO_FILE_CORRUPT;
                case 0x00000780:
                    return PrettyErrorCode.IO_SYSTEM_CANT_ACCESS_FILE;
                case 0x000004C7:
                    return PrettyErrorCode.OPERATION_CANCELLED_BY_USER;
                default:
                    break;
            }

            return PrettyErrorCode.UNKNOWN;
        }

        /// <summary>
        /// Get pretty error code string
        /// </summary>
        /// <param name="preffix">Preffix (must be "SAMPLE_PREFFIX" format)</param>
        /// <param name="ex">Exception</param>
        /// <param name="logger">Logger</param>
        /// <returns>"SAMPLE_PREFFIX_ERROR_400 (0x0)" format string</returns>
        public virtual string GetPrettyErrorCode(string preffix, Exception ex, ILogger? logger)
        {
            string prettyErrorCode;

            var codeObj = MapExceptionToCode(ex, out uint? hr, out int? statusCode);
            var code = codeObj.ToString();
            string _statusCode = statusCode != null ? $"_{statusCode}" : "";

            logger?.CreateErrorLog(nameof(ErrorConvertor), $"{code} - {ex}");
            if (hr != null)
            {
                string hrHex = $"0x{hr.Value:X8}";
                prettyErrorCode = $"{preffix}_{code}{_statusCode} ({hrHex})";
            }
            else
            {
                prettyErrorCode = $"{preffix}_{code}{_statusCode}";
            }
            return prettyErrorCode;
        }

        /// <summary>
        /// Get pretty error code string from windows hcode
        /// </summary>
        /// <param name="preffix">Preffix (must be "SAMPLE_PREFFIX" format)</param>
        /// <param name="hcode">HCode</param>
        /// <param name="logger">Logger</param>
        /// <returns>"SAMPLE_PREFFIX_ERROR_400 (0x0)" format string</returns>
        public virtual string GetPrettyErrorCode(string preffix, int hcode, ILogger? logger)
        {
            string prettyErrorCode;

            PrettyErrorCode prettyCode = ConvertHresultToCode(hcode, out uint? hr);

            var code = prettyCode.ToString();

            logger?.CreateErrorLog(nameof(ErrorConvertor), $"{code} - {hr}");
            if (hr != null)
            {
                string hrHex = $"0x{hr.Value:X8}";
                prettyErrorCode = $"{preffix}_{code} ({hrHex})";
            }
            else
            {
                prettyErrorCode = $"{preffix}_{code}";
            }
            return prettyErrorCode;
        }

        /// <summary>
        /// Get pretty error model
        /// </summary>
        /// <param name="@object">Object, who throws exception</param>
        /// <param name="ex">Exception</param>
        /// <param name="logger">Logger</param>
        /// <returns>Error model</returns>
        public virtual ErrorModel GetErrorModel(string @object, Exception ex, ILogger? logger)
        {
            var codeObj = MapExceptionToCode(ex, out uint? hr, out int? statusCode);
            var code = codeObj.ToString();

            logger?.CreateErrorLog(nameof(ErrorConvertor), $"{code} - {ex}");

            string hrHex = null;

            ErrorModel errorModel = new()
            {
                ErrorCode = code,
                Object = @object,

                StatusCode = statusCode.ToString(),
                Exception = ex,
                HResult = hrHex,
            };

            return errorModel;
        }

    }
}
