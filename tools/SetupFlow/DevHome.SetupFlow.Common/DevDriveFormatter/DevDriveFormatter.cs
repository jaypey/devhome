﻿// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using DevHome.SetupFlow.Common.Helpers;
using Microsoft.Management.Infrastructure;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace DevHome.SetupFlow.Common.DevDriveFormatter;

/// <summary>
/// Class that will perform the format operation to format a partition as a Dev Drive.
/// Note: Due to dependency/build issues when adding System.Management to the ElevatedComponent project
/// this was moved out of the DevDriveStorageOperator class in that project. This is a work around for
/// now until that is fixed.
/// </summary>
public class DevDriveFormatter
{
    /// <summary>
    /// Uses WMI to and the storage Api to format the drive as a Dev Drive. Note: the implementation
    /// is subject to change in the future.
    /// </summary>
    /// <param name="curDriveLetter">The drive letter the method will use when attempting to find a volume and format it</param>
    /// <param name="driveLabel">The new drive label the Dev Drive will have after formatting completes.</param>
    /// <returns>An Hresult as an int that indicates whether the operation succeeded or failed</returns>
    public int FormatPartitionAsDevDrive(char curDriveLetter, string driveLabel)
    {
        Log.Logger?.ReportInfo(Log.Component.DevDrive, nameof(FormatPartitionAsDevDrive), $"Creating CimSession and calling QueryInstances to search for volume whose Drive letter is {curDriveLetter}:");
        try
        {
            // Since at the time of this call the unique object ID of the new volume in unknown,
            // iterate through the volumes that exist to find the one that matches our
            // drive letter. Note: the object ID here is different than what is in AssignDriveLetterToPartition.
            using var session = CimSession.Create(null);
            long fourKb = 4096;
            foreach (var queryObj in session.QueryInstances("root\\Microsoft\\Windows\\Storage", "WQL", "SELECT * from MSFT_Volume"))
            {
                var objectId = queryObj.CimInstanceProperties["ObjectId"].Value as string;
                var letter = queryObj.CimInstanceProperties["DriveLetter"].Value;

                if (letter is char foundALetter
                    && curDriveLetter == foundALetter &&
                    !string.IsNullOrEmpty(objectId))
                {
                    Log.Logger?.ReportInfo(Log.Component.DevDrive, nameof(FormatPartitionAsDevDrive), $"Starting WMI Storage API Format on ObjectId: {objectId} with Driveletter: {curDriveLetter}, using args: DeveloperVolume: true, FileSystem: ReFS, FileSystemLabel: {driveLabel}, AllocationUnitSize: {fourKb}");

                    // Obtain in-parameters for the method
                    var inParams = new CimMethodParametersCollection
                    {
                        // Add the default parameters.
                        CimMethodParameter.Create("DevDrive", true, CimFlags.In),
                        CimMethodParameter.Create("FileSystem", "ReFS", CimFlags.In),
                        CimMethodParameter.Create("FileSystemLabel", driveLabel, CimFlags.In),
                        CimMethodParameter.Create("AllocationUnitSize", fourKb, CimFlags.In),
                    };

                    // Execute the method and obtain the return values.
                    var outParams =
                        session.InvokeMethod(queryObj, "Format", inParams);

                    var returnValue = (uint)outParams.ReturnValue.Value;
                    if (returnValue == 0)
                    {
                        Log.Logger?.ReportInfo(Log.Component.DevDrive, nameof(FormatPartitionAsDevDrive), $"WMI Storage API Format on ObjectId: {objectId} with Driveletter: {curDriveLetter} finished Successfully");
                        return 0;
                    }

                    Log.Logger?.ReportError(Log.Component.DevDrive, nameof(FormatPartitionAsDevDrive), $"WMI Storage API Format on ObjectId: {objectId} with Driveletter: {curDriveLetter}, failed with wmi error: {returnValue}");
                    break;
                }

                var notCorrectDriveLetter = (letter is char ) ? ((char)letter).ToString() : "none";
                Log.Logger?.ReportInfo(Log.Component.DevDrive, nameof(FormatPartitionAsDevDrive), $"CimSession.QueryInstances found ObjectId: {objectId} but its Driveletter: {notCorrectDriveLetter}: is not correct, continuing search...");
            }

            // ReturnValue was not successful. Give this a specific error but this will need
            // to be changed. WMI can return different status and error codes based on the function. The actual returnValue will need
            // to be converted. https://learn.microsoft.com/windows/win32/wmisdk/wmi-return-codes
            var defaultError = (int)PInvoke.HRESULT_FROM_WIN32(WIN32_ERROR.ERROR_FUNCTION_FAILED);
            Log.Logger?.ReportError(Log.Component.DevDrive, nameof(FormatPartitionAsDevDrive), $"Attempt to format drive as a Dev Drive failed default error: 0x{defaultError:X}");
            return defaultError;
        }
        catch (CimException e)
        {
            Log.Logger?.ReportError(Log.Component.DevDrive, nameof(FormatPartitionAsDevDrive), $"A management exception occurred while formating Dev Drive Error.", e);
            return e.HResult;
        }
    }
}
