using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Windows.Web;

namespace GoodbyeDPI_UI.Helper
{
    public class ErrorsHelper
    {
        public enum PrettyErrorCode
        {
            INVALID_URI,
            HOST_NAME_NOT_RESOLVED,
            CANNOT_CONNECT,
            SERVER_UNREACHABLE,
            TIMEOUT,
            CONNECTION_ABORTED,
            CONNECTION_RESET,
            DISCONNECTED,
            OPERATION_CANCELED,
            ERROR_HTTP_INVALID_SERVER_RESPONSE,
            REDIRECT_FAILED,
            UNEXPECTED_STATUS_CODE,
            CERTIFICATE_COMMON_NAME_INCORRECT,
            CERTIFICATE_EXPIRED,
            CERTIFICATE_CONTAINS_ERRORS,
            CERTIFICATE_REVOKED,
            CERTIFICATE_INVALID,
            HTTP_TO_HTTPS_ON_REDIRECTION,
            HTTPS_TO_HTTP_ON_REDIRECTION,
            ACCESS_DENIED,
            ERROR_HTTP_HEADER_NOT_FOUND,

            // IO errors
            IO_FILE_NOT_FOUND,
            IO_DIRECTORY_NOT_FOUND,
            IO_PATH_TOO_LONG,
            IO_ACCESS_DENIED,
            IO_DISK_FULL,
            IO_GENERIC,

            // Extraction errors
            EXTRACT_INVALID_ARCHIVE,
            EXTRACT_ENTRY_CORRUPTED,
            EXTRACT_UNKNOWN,

            UNKNOWN,
        }
        public class ErrorHelper
        {
            public static PrettyErrorCode? MapExceptionToCode(Exception ex, out uint? rawHResult)
            {
                rawHResult = null;
                for (Exception current = ex; current != null; current = current.InnerException)
                {
                    if (current is WebSocketException wsEx)
                    {
                        var status = Windows.Networking.Sockets.WebSocketError.GetStatus(wsEx.HResult);
                        switch (status)
                        {
                            case WebErrorStatus.HostNameNotResolved:
                                return PrettyErrorCode.HOST_NAME_NOT_RESOLVED;
                            case WebErrorStatus.CannotConnect:
                                return PrettyErrorCode.CANNOT_CONNECT;
                            case WebErrorStatus.ServerUnreachable:
                                return PrettyErrorCode.SERVER_UNREACHABLE;
                            case WebErrorStatus.Timeout:
                                return PrettyErrorCode.TIMEOUT;
                            case WebErrorStatus.ConnectionAborted:
                                return PrettyErrorCode.CONNECTION_ABORTED;
                            case WebErrorStatus.ConnectionReset:
                                return PrettyErrorCode.CONNECTION_RESET;
                            case WebErrorStatus.Disconnected:
                                return PrettyErrorCode.DISCONNECTED;
                            case WebErrorStatus.OperationCanceled:
                                return PrettyErrorCode.OPERATION_CANCELED;
                            case WebErrorStatus.ErrorHttpInvalidServerResponse:
                                return PrettyErrorCode.ERROR_HTTP_INVALID_SERVER_RESPONSE;
                            case WebErrorStatus.RedirectFailed:
                                return PrettyErrorCode.REDIRECT_FAILED;
                            case WebErrorStatus.UnexpectedStatusCode:
                                return PrettyErrorCode.UNEXPECTED_STATUS_CODE;
                            case WebErrorStatus.CertificateCommonNameIsIncorrect:
                                return PrettyErrorCode.CERTIFICATE_COMMON_NAME_INCORRECT;
                            case WebErrorStatus.CertificateExpired:
                                return PrettyErrorCode.CERTIFICATE_EXPIRED;
                            case WebErrorStatus.CertificateContainsErrors:
                                return PrettyErrorCode.CERTIFICATE_CONTAINS_ERRORS;
                            case WebErrorStatus.CertificateRevoked:
                                return PrettyErrorCode.CERTIFICATE_REVOKED;
                            case WebErrorStatus.CertificateIsInvalid:
                                return PrettyErrorCode.CERTIFICATE_INVALID;
                            case WebErrorStatus.HttpToHttpsOnRedirection:
                                return PrettyErrorCode.HTTP_TO_HTTPS_ON_REDIRECTION;
                            case WebErrorStatus.HttpsToHttpOnRedirection:
                                return PrettyErrorCode.HTTPS_TO_HTTP_ON_REDIRECTION;
                            default:
                                break;
                        }
                    }
                    if (current is UriFormatException)
                        return PrettyErrorCode.INVALID_URI;
                    if (current is HttpRequestException httpEx && httpEx.StatusCode.HasValue)
                        return PrettyErrorCode.UNEXPECTED_STATUS_CODE;

                    if (current is FileNotFoundException)
                        return PrettyErrorCode.IO_FILE_NOT_FOUND;
                    if (current is DirectoryNotFoundException)
                        return PrettyErrorCode.IO_DIRECTORY_NOT_FOUND;
                    if (current is PathTooLongException)
                        return PrettyErrorCode.IO_PATH_TOO_LONG;
                    if (current is UnauthorizedAccessException)
                        return PrettyErrorCode.IO_ACCESS_DENIED;
                    if (current is IOException)
                    {
                        var hrGeneric = unchecked((uint)current.HResult);
                        if (hrGeneric == 0x80070070u)
                            return PrettyErrorCode.IO_DISK_FULL;
                        return PrettyErrorCode.IO_GENERIC;
                    }
                    if (current is InvalidDataException)
                        return PrettyErrorCode.EXTRACT_INVALID_ARCHIVE;
                    if (current is IOException && ex.Message.Contains("corrupt", StringComparison.OrdinalIgnoreCase))
                        return PrettyErrorCode.EXTRACT_ENTRY_CORRUPTED;

                    Debug.WriteLine(unchecked((uint)current.HResult));
                    if (current.HResult != 0)
                    {
                        uint hr = unchecked((uint)current.HResult);
                        rawHResult = hr;
                        switch (hr)
                        {
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
                            default:
                                break;
                        }
                    }
                }
                if (ex is UriFormatException)
                    return PrettyErrorCode.INVALID_URI;
                if (ex is TimeoutException)
                    return PrettyErrorCode.TIMEOUT;
                return PrettyErrorCode.UNKNOWN;
            }
        }
    }
}
