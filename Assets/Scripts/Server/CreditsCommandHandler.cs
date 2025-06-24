using System.Collections.Specialized;
using System.Net;
using System.Text;
using UnityEngine;

public class CreditsCommandHandler : HttpCommandHandlerBase {
    public override void HandleCommand(HttpListenerContext context, NameValueCollection query) {
        var responseData = new ServerResponse();

        // クレジット情報をJSON文字列として構築
        string creditsJson = @"{
""unity"": {
""name"": ""Unity Technologies"",
""license"": ""© 2024 Unity Software Inc. All rights reserved."",
""url"": ""https://unity.com/legal""
},
""vroid"": {
""name"": ""VRoid Studio"",
""license"": ""Character created with VRoid Studio. © pixiv Inc."",
""url"": ""https://vroid.com/en/studio/license""
},
""anime_girl_idle"": {
""name"": ""Anime Girl Idle Animation"",
""license"": ""© Clean Curve Studio, Standard Unity Asset Store EULA"",
""url"": ""https://assetstore.unity.com/packages/3d/animations/anime-girl-idle-animations-150397""
},
""univrm"": {
""name"": ""UniVRM"",
""license"": ""©2024 DWANGO Co., Ltd. MIT License"",
""url"": ""https://github.com/vrm-c/UniVRM""
},
""unigltf"": {
""name"": ""UniGLTF"",
""license"": ""MIT License"",
""url"": ""https://github.com/ousttrue/UniGLTF""
},
""CSCore"": {
""name"": ""CSCore.CoreAudioAPI/CSCore.SoundIn"",
""license"": ""Microsoft Public License (Ms-PL)"",
""url"": ""https://github.com/filoe/cscore/blob/master/license.md""
},
""vhost"": {
""name"": ""General VRM Agent Host"",
""license"": ""💖MAG^23:Presented by 🍁Maple and the 🌌Galaxy exponent 23💫"",
""url"": ""https://linktr.ee/mag_exp_23""
}
}";


        responseData.status = 200;
        responseData.succeeded = true;
        responseData.message = creditsJson.Replace("\r\n", "").Replace("\\", "");

        SendResponse(context, responseData);
    }
}
