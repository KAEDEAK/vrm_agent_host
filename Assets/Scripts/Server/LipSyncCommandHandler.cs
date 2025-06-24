using System.Collections.Specialized;
using System.Net;

public class LipSyncCommandHandler : HttpCommandHandlerBase {
    private AudioLipSync _lipSync;

    private static readonly string[] AllowedCommands = {
        "getstatus", "audiosync", "audiosync_on", "audiosync_off"
    };

    public LipSyncCommandHandler(AudioLipSync lipSync) {
        _lipSync = lipSync;
    }

    public override void HandleCommand(HttpListenerContext context, NameValueCollection query) {
        var responseData = new ServerResponse();

        string cmd = GetCommandName(query, AllowedCommands, out bool ok);
        if (!ok) {
            responseData.status = 400;
            responseData.message = string.Format(i18nMsg.ERROR_INVALID_LIPSYNC_CMD, cmd);
            SendResponse(context, responseData);
            return;
        }


        switch (cmd) {
            case "getstatus":
                if (!_lipSync.IsInitialized) {
                    responseData.status = 503;
                    responseData.message = i18nMsg.RESPONSE_LIPSYNC_NOT_INITIALIZED;
                }
                else {
                    responseData.status = 200;
                    responseData.message = new RawJson(_lipSync.GetLipSyncStatusJson());
                }
                break;

            case "audiosync":
            case "audiosync_on":
                // Default to microphone channel for backward compatibility
                int channel = GetQueryInt(query, "channel", 2);
                float scale = GetQueryFloat(query, "scale", 3.0f);

                _lipSync.StartLipSync(channel, scale);
                responseData.status = 200;
                responseData.message = string.Format(i18nMsg.RESPONSE_LIPSYNC_ON, channel);
                //responseData.message = string.Format(i18nMsg.AUDIOSYNC_ON_RESPONSE, channel);
                break;

            case "audiosync_off":
                _lipSync.StopLipSync();
                responseData.status = 200;
                responseData.message = i18nMsg.RESPONSE_LIPSYNC_OFF;
                //responseData.message = i18nMsg.AUDIOSYNC_OFF_RESPONSE;
                break;
        }

        SendResponse(context, responseData);
    }
}
