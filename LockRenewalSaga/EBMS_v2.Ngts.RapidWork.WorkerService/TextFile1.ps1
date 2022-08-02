[Reflection.Assembly]::LoadWithPartialName("System.Web")| out-null

 

#command line: .\initiatecatrun.ps1 <keyName> <key>

 

$URI=https://micktestasdf.servicebus.windows.net/ngts.catrun.test

$type="Ngts.CatRun.BusOrchestration.AnnualTaxRun, Ngts.CatRun.BusOrchestration, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"

 

$Access_Policy_Name=$args[0] #keyName

$Access_Policy_Key=$args[1]  #key

$FullUri = $URI + "/messages"

$Expires=([DateTimeOffset]::Now.ToUnixTimeSeconds())+(60*60*24)

$SignatureString=[System.Web.HttpUtility]::UrlEncode($URI)+ "`n" + [string]$Expires

$HMAC = New-Object System.Security.Cryptography.HMACSHA256

$HMAC.key = [Text.Encoding]::ASCII.GetBytes($Access_Policy_Key)

$Signature = $HMAC.ComputeHash([Text.Encoding]::ASCII.GetBytes($SignatureString))

$Signature = [Convert]::ToBase64String($Signature)

$SASToken = "SharedAccessSignature sr=" + [System.Web.HttpUtility]::UrlEncode($URI) + "&sig=" + [System.Web.HttpUtility]::UrlEncode($Signature) + "&se=" + $Expires + "&skn=" + $Access_Policy_Name

$ShortDate = [DateTime]::Now.AddDays(-1).ToString("MM/dd/yyyy hh:mm tt")

$ProgressPreference = "SilentlyContinue"

$CurlResponse = curl -Uri $FullUri -Method Post -ContentType 'application/json' `

    -Header @{

        "Authorization" = $SASToken;

        "NServiceBus.Transport.Encoding" = "application/octect-stream";

        "NServiceBus.MessageId" = [Guid]::NewGuid();

        "NServiceBus.MessageIntent" = "Publish";

        "NServiceBus.ConversationId" = [Guid]::NewGuid();

        "NServiceBus.CorrelationId" = [Guid]::NewGuid();

        "NServiceBus.OriginatingMachine" = $env:COMPUTERNAME;

        "NServiceBus.OriginatingEndpoint" = "TIDAL_CatRun";

        "NServiceBus.EnclosedMessageTypes" = $type;

        "NServiceBus.TimeSent" = [DateTime]::UtcNow.ToString("o")

        } `

    -Body "{}" -UseBasicParsing

$CurlResponse.RawContent

$CurlResponse.Content