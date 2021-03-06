/*

	This script creates the CLR functions in a SQL database from the OpenPseudonymiser CryptoLib DLL
	
	There are two things you will probably need to change in this script before you run it:
	1) The name of your database
	2) The location of your openP DLL	
	Both of the above items have the comment /**CHANGE ME**/ to make them easy to find
	
	When the script has been you you will have four new functions in your database, examles of how to use them:
	
	SELECT dbo.GetDigest('StringYouWantToDigest', 'PlainTextSalt')
	SELECT dbo.ProcessNHSNumber('3334.223.112')
	SELECT dbo.RoundDownDate('12/12/2012')
	SELECT dbo.IsValidNHSNumber('4505577104')
	
	You can chain the functions together, this is particularly useful if you want to strip all non numerics from an NHS Number before processing or validating it. E.g:
	SELECT dbo.GetDigest(dbo.ProcessNHSNumber('450 557 7104'),'salt')
	SELECT dbo.IsValidNHSNumber(dbo.ProcessNHSNumber('450 557 7104'))
	
	If you have the example database that comes from the OpenPseudonymiser site (www.openspeudonymiser.org) called OpenP_CLR 
	then this is how you would call use the functions on a table of data:
	
	SELECT	dbo.GetDigest(dbo.ProcessNHSNumber(NHSNumber), 'PlainTextSalt') as Digest,
			dbo.RoundDownDate(DateOfBirth) as DateOfBirth,
			dbo.IsValidNHSNumber(dbo.ProcessNHSNumber(NHSNumber)) as IsValidNHS,
			[Name]
		FROM ExampleData
			
	
*/


--USE OpenP_JC	/**CHANGE ME**/
--GO
--sp_configure 'clr enabled', 1
--go

--/* Trustworthy is required for the CLR  file system access to work in the GetDigestUsingEncryptedSaltFile function */
--alter database [OpenP_CLR] SET Trustworthy on


--RECONFIGURE;
--go


IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GetDigest]') AND type in (N'FN', N'IF', N'TF', N'FS', N'FT'))
BEGIN
	DROP FUNCTION [dbo].[GetDigest]
END
GO

IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RoundDownDate]') AND type in (N'FN', N'IF', N'TF', N'FS', N'FT'))
BEGIN
	DROP FUNCTION [dbo].RoundDownDate
END
GO
IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProcessNHSNumber]') AND type in (N'FN', N'IF', N'TF', N'FS', N'FT'))
BEGIN
	DROP FUNCTION [dbo].ProcessNHSNumber
END
GO
IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[IsValidNHSNumber]') AND type in (N'FN', N'IF', N'TF', N'FS', N'FT'))
BEGIN
	DROP FUNCTION [dbo].IsValidNHSNumber
END
GO

IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GetFile]') AND type in (N'FN', N'IF', N'TF', N'FS', N'FT'))
BEGIN
	DROP FUNCTION [dbo].GetFile
END
GO


IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GetDigestUsingEncryptedSaltFile]') AND type in (N'FN', N'IF', N'TF', N'FS', N'FT'))
BEGIN
	DROP FUNCTION [dbo].GetDigestUsingEncryptedSaltFile
END
GO


IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GetDigestUsingStoredEncryptedSalt]') AND type in (N'FN', N'IF', N'TF', N'FS', N'FT'))
BEGIN
	DROP FUNCTION [dbo].GetDigestUsingStoredEncryptedSalt
END
GO



IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[StoreEncryptedSalt]') AND type in (N'P', N'PC'))
BEGIN
	DROP PROCEDURE [dbo].[StoreEncryptedSalt]
END
GO

 



IF  EXISTS (SELECT * FROM sys.assemblies asms WHERE asms.name = N'OpenPseudonymiser_CryptoLib')
BEGIN
	DROP ASSEMBLY [OpenPseudonymiser_CryptoLib]
END


IF  EXISTS (SELECT * FROM sys.assemblies asms WHERE asms.name = N'RSAEncryptionLib')
BEGIN
	DROP ASSEMBLY [RSAEncryptionLib]
END

GO


/*

Might need to do this too:

USE [OpenP_CLR_0.9.7]
GO
EXEC dbo.sp_changedbowner @loginame = N'sa', @map = false
GO

*/

CREATE ASSEMBLY [RSAEncryptionLib]
AUTHORIZATION [dbo]
FROM 'C:\Code_OpenP\dotnet-src\Crypto\bin\Release\RSAEncryptionLib.dll'			/**CHANGE ME**/
WITH PERMISSION_SET = external_access

	GO

CREATE ASSEMBLY [OpenPseudonymiser_CryptoLib]
AUTHORIZATION [dbo]
FROM 'C:\Code_OpenP\dotnet-src\Crypto\bin\Release\OpenPseudonymiser_CryptoLib.dll'/**CHANGE ME**/
WITH PERMISSION_SET = external_access



GO

CREATE FUNCTION dbo.GetDigest (@inputString nvarchar(4000), @salt nvarchar(4000))
RETURNS nvarchar(64)
AS
EXTERNAL NAME OpenPseudonymiser_CryptoLib.SQLCrypto.GetDigest
GO

SELECT dbo.GetDigest('ham', 'eggs')

GO

CREATE FUNCTION dbo.ProcessNHSNumber (@inputString nvarchar(100))
RETURNS nvarchar(100)
AS
EXTERNAL NAME OpenPseudonymiser_CryptoLib.SQLCrypto.ProcessNHSNumber 
GO

SELECT dbo.ProcessNHSNumber('ham.33.34')

GO
CREATE FUNCTION dbo.RoundDownDate (@inputString nvarchar(100))
RETURNS nvarchar(100)
AS
EXTERNAL NAME OpenPseudonymiser_CryptoLib.SQLCrypto.RoundDownDate 
GO

/*
	Test the formats of date supported
	i.e.   "yyyyMMdd", "dd/MM/yy", "dd/MM/yyyy", "dd.MM.yy", "dd.MM.yyyy" 
	Use 12 Dec 2012, they should all return "20120101"
*/


SELECT dbo.RoundDownDate('20121215')
SELECT dbo.RoundDownDate('15/12/12')
SELECT dbo.RoundDownDate('12/12/2012')
SELECT dbo.RoundDownDate('15.12.12')
SELECT dbo.RoundDownDate('12.12.2012')


GO

CREATE FUNCTION dbo.IsValidNHSNumber (@inputString nvarchar(100))
RETURNS bit
AS
EXTERNAL NAME OpenPseudonymiser_CryptoLib.SQLCrypto.IsValidNHSNumber 
GO

SELECT dbo.IsValidNHSNumber(dbo.ProcessNHSNumber('450 557 7104'))

SELECT dbo.IsValidNHSNumber('4505577104')

GO


CREATE FUNCTION dbo.GetDigestUsingEncryptedSaltFile (@inputString nvarchar(100), @locationOfFile nvarchar(255))
RETURNS nvarchar(2000)
AS
EXTERNAL NAME OpenPseudonymiser_CryptoLib.SQLCrypto.GetDigestUsingEncryptedSaltFile 
GO


/* should yield the same result with encrypted and non encrpted versions of the salt*/
--Select dbo.GetDigest('foo', 'pie')
--Select dbo.GetDigestUsingEncryptedSaltFile('foo', 'C:\Users\Public\thewordPie.EncryptedSalt')

/*

Select top 10000 OPENP_CLR.dbo.GetDigestUsingEncryptedSaltFile([PATIENT_ID], 'C:\Users\Public\thewordPie.EncryptedSalt')
, [PATIENT_ID]
  FROM [Nato_Jan2011_Filer3].[dbo].[OBSERVATIONS]
GO
select top 10000 [PATIENT_ID], [PATIENT_ID]  FROM [Nato_Jan2011_Filer3].[dbo].[OBSERVATIONS]

--  14 seconds for straght digest get of 10,000 rows

Select top 10000 
OPENP_CLR.dbo.GetDigestUsingEncryptedSaltFile(OPENP_CLR.dbo.ProcessNHSNumber ([PATIENT_ID]), 'C:\Users\Public\thewordPie.EncryptedSalt')
, [PATIENT_ID]
  FROM [Nato_Jan2011_Filer3].[dbo].[OBSERVATIONS]
--  14 seconds for addition of NHSNumber processing



Select top 10000 
OPENP_CLR.dbo.ProcessNHSNumber ([PATIENT_ID])
, [PATIENT_ID]
  FROM [Nato_Jan2011_Filer3].[dbo].[OBSERVATIONS]
--  negligable seconds for straight NHSNumber processing


Select top 100000
OPENP_CLR.dbo.GetDigest([PATIENT_ID], 'pie')
, [PATIENT_ID]
  FROM [Nato_Jan2011_Filer3].[dbo].[OBSERVATIONS]
--  negligable seconds for plain text salt digest

*/


CREATE FUNCTION dbo.GetDigestUsingStoredEncryptedSalt (@inputString nvarchar(100))
RETURNS nvarchar(2000)
AS
EXTERNAL NAME OpenPseudonymiser_CryptoLib.SQLCrypto.GetDigestUsingStoredEncryptedSalt 
GO



go
CREATE PROCEDURE dbo.StoreEncryptedSalt (@locationOfFile nvarchar(255))
AS
EXTERNAL NAME OpenPseudonymiser_CryptoLib.SQLCrypto.StoreEncryptedSalt
GO


/*
	If the storage of the encrypted salt fails you will get a message:
	"Msg 10312, Level 16, State 49, Procedure StoreEncryptedSalt, Line 0
.NET Framework execution was aborted. The UDP/UDF/UDT did not revert thread token."

Also you will not the LastSavedAt time stamp on the EncryptedSalt table has not been updated to the current time
SELECT * from [dbo].[EncryptedSalt]


)
*/

--DROP TABLE [dbo].[EncryptedSalt]



--CREATE TABLE [dbo].[EncryptedSalt](
--	[EncryptedSalt] [varbinary](max) NOT NULL,
--	[LastSavedAt] [datetime] NOT NU		LL
--) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

--GO

EXECUTE [dbo].[StoreEncryptedSalt] @locationOfFile = 'C:\Users\Public\theword_Pie.EncryptedSalt'
GO

-- so the three ways of getting a digest should all return the same thing now we have stored the above encrypted salt

Select dbo.GetDigest('foo', 'pie')
--Select dbo.GetDigestUsingEncryptedSaltFile('foo', 'C:\Users\Public\thewordPie.EncryptedSalt')
Select dbo.GetDigestUsingStoredEncryptedSalt('foo')


/*
Select top 10000
OPENP_CLR.dbo.GetDigestUsingStoredEncryptedSalt(([PATIENT_ID]))
, [PATIENT_ID]
  FROM [Nato_Jan2011_Filer3].[dbo].[OBSERVATIONS]
--  6 seconds for salt from preprepared file
- seems most of the time is in the decrypting, not the reading of the file
*/


/*
GO


also commented out in the assembly, left in case perf testing means we need to load the salt first then run through the rows with it ready..

create function GetFile(@fn nvarchar(300))
returns table(filecontents varbinary(max))
as external name OpenPseudonymiser_CryptoLib.SQLCrypto.GetFile

go

 Select *
from dbo.GetFile('C:\Users\Public\encryptedSalt.txt')


Following are some simple test queries 
 select *
from GetFile('C:\MyDir\function1.cs')
go
 select *
from GetFile('NonExistentFile.cs') – should return NULL
*/