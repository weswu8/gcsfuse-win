GcsFuse-Win
=====

GcsFuse-Win is a distributed [FUSE](http://fuse.sourceforge.net) based file system backed by [Google cloud storage service](https://cloud.google.com/storage). It allows you to mount the buckets/folders in the storage account as a the local folder/driver on Windows system. It support the cluster mode. you can mount the blob container (or part of it) across multiple windows nodes.

## Important Notes:
* this tools is still in developing status, you can use at your own risk.

## Project Goals
Object storage is one of the most fundamental topic you'll encounter when you decide to start your cloud journey.The main goal of the project is to make gcp storage service easy to use for windows box.

## Key Updates:

### release v1.0.0

* Optimize performance for large content reads and writes
* Optimize performance for multiple files concurrently read/write


## Features:
* Implemented these winfsp functions: GetVolumeInfo,SetVolumeLabel, GetSecurityByName, GetFileInfo, open, Create, Read, Write, Cleanup, Close, SetBasicInfo, SetFileSize, CanDelete, Rename, Overwrite, ReadDirectoryEntry, GetDirInfoByName.
* Allow mount multiple buckets (or part of them) as the local folder.
* The contents are pre-cached by chunks when there is read operation. This will eliminate the times of http request and increase the performance greatly. 
* Gcs resumable uploads are used for the write operation. Data is buffered firstly and then be uploaded if the buffer size exceed the threshold. This also can eliminate the times of http request and increase the performance greatly. 
* You can edit the file content on the fly, especially recommend for the small file, It does not need download, edit and then upload.
* Append mode is supported, you can append the new line to the existing blob directly. this is more friendly for logging operation.
* Use server-side copy for move, rename operations, more efficient for big files and folders.

## Architecture and introduction
* ToDo

## installation
Installation now is very easy. But I strongly recommend to test and verify it in you environment before you use it. it's at your own risk.
### Precondition
	you should have a gcp account, and you should create and download the service account json file.[how to do this?](https://cloud.google.com/docs/authentication/getting-started). pls put the file into the conf/ directory
### 1.Install WinFsp
    Download the winfsp-1.7.20038.msi from [WinFsp](https://github.com/billziss-gh/winfsp/releases).
	lanuch it and install with default configuration.
### 2.Install blobfs-win
#### 2.1 Download the blobfs-win released version.
#### 2.2 Edit configuration file: 
	Open conf/config.xml
	change the setting of :
		<ProjectId>gcp_project_id</ProjectId>
		<!--  Gcp service account json file --> 
		<ServiceAccountFile>gcp_service_account.json</ServiceAccountFile>
		<!--  the prefix of target buckets or objects, use / will mount all buckets --> 
		<RootPrefix>/bucket_name/</RootPrefix>
		<!--  the dirve letter of local host --> 
		<MountDrive>F:</MountDrive>
		<!--  cached object TTL in seconds --> 
		<CacheTTL>180</CacheTTL>

### final.Start the blobfs service
    lanuch gcsfuse-win.exe
	
It is highly recommended that you should config it as a windows services.

## Tips
* the block blob is read only by default. marked with read only flag in the popup properties windows.
# the size number of the mounted folder is not real.


## Performance Test
* The performance depends on the machine and the network. for the VMs within the same region with the blob service, The average bandwidth is 20 ~ 30MB/s

## Dependency
* [WinFsp](https://github.com/billziss-gh/winfsp): Great Windows File System Proxy - FUSE for Windows.


## Limitation and known issues:
* Due to the overhead of fuse system, the performance will be expected slower than native file system. 
* For the file copy, the blobfs will use read out - then write in to new blob mode. this will spent more time for large files/folders.
* In some cases for the desktop user, right-click these files (*.PPT(X), *.DOC(X)) may casue very slow response.
* In Windows UI, copy the folder with many small files will be very slow. you can zip it. use robocopy or other tools.

## Supported platforms
* windows


## Command Line Usage
	usage: Blobfs-Win OPTIONS

	options:
		-d DebugFlags       [-1: enable all debug logs]
		-D DebugLogFile     [file path; use - for stderr]
		-i                  [case insensitive file system]
		-t FileInfoTimeout  [millis]
		-n MaxFileNodes
		-s MaxFileSize      [bytes]
		-F FileSystemName]
		-m MountPoint       [X:|* (required if no UNC prefix)]s

## License
	Copyright (C) 2020 Wesley Wu jie1975.wu@gmail.com
	This code is licensed under The General Public License version 3
	
## FeedBack
	Your feedbacks are highly appreciated! :)
