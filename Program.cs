/*
MIT License

Copyright (c) 2018 Warren Ashcroft

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using WebSocketSharp;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp
{
    #region LightwaveRf Web Socket Types
    [DataContract]
    public class LightwaveRfWebsocketMessage
    {
        private static int _TRANSACTION_ID = 0;
        private static string _SENDER_ID = Guid.NewGuid().ToString();

        public LightwaveRfWebsocketMessage(string opclass, string operation)
        {
            this.opclass = opclass;
            this.operation = operation;
            this.direction = "request";

            this.version = 1;
            this.senderId = _SENDER_ID;
            this.transactionId = _TRANSACTION_ID;

            _TRANSACTION_ID++;
        }

        [DataMember(Name = "class")]
        public string opclass { get; set; }

        [DataMember(Name = "direction")]
        public string direction { get; set; }

        [DataMember(Name = "operation")]
        public string operation { get; set; }

        [DataMember(Name = "senderId")]
        public string senderId { get; set; }

        [DataMember(Name = "transactionId")]
        public int transactionId { get; set; }

        [DataMember(Name = "version")]
        public int version { get; set; }

        [DataMember(Name = "items")]
        public LightwaveRfWebsocketMessageItem[] items { get; set; }

        [IgnoreDataMember]
        public TaskCompletionSource<LightwaveRfWebsocketMessage> requestTask { get; internal set; }
    }

    [DataContract]
    public class LightwaveRfWebsocketMessageItem
    {
        private static int _ITEM_ID = 0;

        public LightwaveRfWebsocketMessageItem()
        {
            this.itemId = _ITEM_ID;

            _ITEM_ID++;
        }

        [DataMember(Name = "itemId")]
        public int itemId { get; set; }

        [DataMember(Name = "payload")]
        public dynamic payload { get; set; }

        [DataMember(Name = "success", EmitDefaultValue = false)]
        public bool? success { get; set; }

        [DataMember(Name = "error", EmitDefaultValue = false)]
        public LightwaveRfWebsocketMessageItemError error { get; set; }
    }

    [DataContract]
    public class LightwaveRfWebsocketMessageItemError
    {
        [DataMember(Name = "code")]
        public int code { get; set; }

        [DataMember(Name = "group")]
        public bool group { get; set; }

        [DataMember(Name = "source")]
        public string source { get; set; }

        [DataMember(Name = "message")]
        public string message { get; set; }
    }

    [DataContract]
    public class LightwaveRfDevice
    {
        [DataMember(Name = "deviceId")]
        public string deviceId { get; set; }

        [DataMember(Name = "name")]
        public string name { get; set; }

        [DataMember(Name = "productCode")]
        public string productCode { get; set; }

        [DataMember(Name = "featureIds")]
        public string[] featureIds { get; set; }

        [DataMember(Name = "featureSetGroupIds")]
        public string[] featureSetGroupIds { get; set; }

        [DataMember(Name = "paired", EmitDefaultValue = false)]
        public bool? paired { get; set; }

        [DataMember(Name = "createdDate", EmitDefaultValue = false)]
        public DateTime? createdDate { get; set; }
    }

    [DataContract]
    public class LightwaveRfFeature
    {
        [DataMember(Name = "featureId")]
        public string featureId { get; set; }

        [DataMember(Name = "deviceId")]
        public string deviceId { get; set; }

        [DataMember(Name = "name")]
        public string name { get; set; }

        [DataMember(Name = "groups")]
        public string[] groups { get; set; }

        [DataMember(Name = "attributes", EmitDefaultValue = false)]
        public LightwaveRfAttributes attributes { get; set; }

        [DataMember(Name = "paired", EmitDefaultValue = false)]
        public string paired { get; set; }

        [DataMember(Name = "createdDate", EmitDefaultValue = false)]
        public DateTime? createdDate { get; set; }
    }

    [DataContract]
    public class LightwaveRfAttributes
    {
        [DataMember(Name = "featureId")]
        public int featureId { get; set; }

        [DataMember(Name = "channel")]
        public int channel { get; set; }

        [DataMember(Name = "name")]
        public string name { get; set; }

        [DataMember(Name = "type")]
        public string type { get; set; }

        [DataMember(Name = "value")]
        public int value { get; set; }

        [DataMember(Name = "status")]
        public string status { get; set; }

        [DataMember(Name = "writeable", EmitDefaultValue = false)]
        public bool? writeable { get; set; }
    }
    #endregion

    public class Program
    {
        private static string _EMAIL = "EMAIL_ADDRESS_HERE";
        private static string _PASSWORD = "PASSWORD_HERE";

        private static string _DEVICE_ID = Guid.NewGuid().ToString();
        private static WebSocket _WEBSOCKET = new WebSocket("wss://v1-linkplus-app.lightwaverf.com");

        private static Dictionary<string, string> _FEATURESETS = new Dictionary<string, string>();
        private static Dictionary<string, LightwaveRfDevice> _DEVICES = new Dictionary<string, LightwaveRfDevice>();
        private static Dictionary<string, LightwaveRfFeature> _FEATURES = new Dictionary<string, LightwaveRfFeature>();
        private static Dictionary<int, LightwaveRfWebsocketMessage> _TRANSACTIONS = new Dictionary<int, LightwaveRfWebsocketMessage>();

        public static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
            Console.ReadKey();
        }

        public static async Task MainAsync(string[] args)
        {
            // Connect WebSocket
            _WEBSOCKET.OnMessage += (sender, e) => WebSocketReceiveMessage(e.Data);
            // _WEBSOCKET.SetProxy("http://127.0.0.1:8888", "", "");
            _WEBSOCKET.Connect();

            // Authenticate and load system data
            await LightwaveRfAuthenticateAsync();
            await LightwaveRfReadRootGroupsAsync();

            // EXAMPLES
            int value = 0;
            Console.WriteLine("");

            string deviceName = "Master Bedroom";
            string switchName = "Master Bedroom Switch 2";

            string deviceId = _DEVICES.Values.Where(device => device.name == deviceName).Single().deviceId;

            var switchFeatures = _FEATURES.Values.Where(v => v.deviceId == deviceId && v.attributes.type == "switch"); // Two results on a 2-gang switch
            var switchFeatureId = switchFeatures.Where(v => _FEATURESETS[v.featureId] == switchName).Single().featureId; // Found the correct one by its user specified name

            var dimFeatures = _FEATURES.Values.Where(v => v.deviceId == deviceId && v.attributes.type == "dimLevel"); // Two results on a 2-gang switch
            var dimFeatureId = dimFeatures.Where(v => _FEATURESETS[v.featureId] == switchName).Single().featureId; // Found the correct one by its user specified name


            // Read switch
            await LightwaveRfLightwaveRfReadFeaturesAsync(switchFeatureId);
            value = _FEATURES[switchFeatureId].attributes.value;
            Console.WriteLine("EXAMPLE: Switch is currently switched " + (value == 1 ? "on" : "off"));

            // Set switch
            value = 1;
            await LightwaveRfLightwaveRfWriteFeatureAsync(switchFeatureId, value); // 0 = Off, 1 = On
            Console.WriteLine("EXAMPLE: Switch has now been switched " + (value == 1 ? "on" : "off"));
            
            Console.WriteLine("");

            // Read dimming level
            await LightwaveRfLightwaveRfReadFeaturesAsync(dimFeatureId);
            value = _FEATURES[dimFeatureId].attributes.value;
            Console.WriteLine("EXAMPLE: Switch is currently dimmed to " + value + "%");

            // Set dimming level
            value = 75;
            await LightwaveRfLightwaveRfWriteFeatureAsync(dimFeatureId, value); // Dimmed to 75%
            Console.WriteLine("EXAMPLE: Switch has now been dimmed to " + value + "%");
        }

        private static async Task<LightwaveRfWebsocketMessage> WebSocketSendMessageAsync(LightwaveRfWebsocketMessage message)
        {
            message.requestTask = new TaskCompletionSource<LightwaveRfWebsocketMessage>();

            _TRANSACTIONS.Add(message.transactionId, message);
            _WEBSOCKET.Send(JsonConvert.SerializeObject(message));

            await message.requestTask.Task;
            return message.requestTask.Task.Result;
        }

        private static void WebSocketReceiveMessage(string data)
        {
            LightwaveRfWebsocketMessage sentMessage = null;
            LightwaveRfWebsocketMessage receivedMessage = null;

            try
            {
                receivedMessage = JsonConvert.DeserializeObject<LightwaveRfWebsocketMessage>(data);
            }
            catch
            {
                return;
            }

            if (receivedMessage.opclass == "feature" && (receivedMessage.operation == "read" || receivedMessage.operation == "write"))
            {
                // This is a workaround for the protocol/API bug described below
                var itemId = receivedMessage.items[0].itemId;
                var transactionId = _TRANSACTIONS.Values.Single(v => v.items.All(x => x.itemId == itemId)).transactionId;
                receivedMessage.transactionId = transactionId;
            }

            if (_TRANSACTIONS.TryGetValue(receivedMessage.transactionId, out sentMessage))
            {
                _TRANSACTIONS.Remove(receivedMessage.transactionId);
                sentMessage.requestTask.SetResult(receivedMessage);
            }
            else
            {
                if (receivedMessage.direction == "notification")
                {
                    if (receivedMessage.opclass == "group" && receivedMessage.operation == "update")
                    {
                        LightwaveRfReadRootGroupsAsync();
                        return;
                    }
                    else if (receivedMessage.operation == "event")
                    {
                        LightwaveRfEventHandler(receivedMessage);
                        return;
                    }
                }

                Console.WriteLine("Unexpected Response: " + data);
            }
        }

        public static async Task<bool> LightwaveRfAuthenticateAsync()
        {
            string accessToken = await GetAccessTokenAsync(_EMAIL, _PASSWORD);

            var response = await WebSocketSendMessageAsync(new LightwaveRfWebsocketMessage("user", "authenticate")
            {
                items = new[]
                {
                        new LightwaveRfWebsocketMessageItem
                        {
                            payload = new { clientDeviceId = _DEVICE_ID, token = accessToken }
                        }
                }
            });

            foreach (var item in response.items)
            {
                if (!item.success.GetValueOrDefault())
                {
                    Console.WriteLine("Authentication Failed");
                    return false;
                }

                Console.WriteLine("Authentication Succeeded");
            }

            return true;
        }

        public static async Task<bool> LightwaveRfReadRootGroupsAsync()
        {
            var response = await WebSocketSendMessageAsync(new LightwaveRfWebsocketMessage("user", "rootGroups")
            {
                items = new[]
                {
                        new LightwaveRfWebsocketMessageItem()
                }
            });

            foreach (var item in response.items)
            {
                if (!item.success.GetValueOrDefault())
                {
                    Console.WriteLine("Read Root Groups Failed");
                    return false;
                }

                Console.WriteLine("Read Root Groups Succeeded");

                var groupIds = (item.payload.groupIds as JArray).ToObject<string[]>();

                await LightwaveRfReadGroupsAsync(groupIds);
                await LightwaveRfLightwaveRfReadHierarchyAsync(groupIds);
            }

            return true;
        }

        public static async Task<bool> LightwaveRfReadGroupsAsync(string[] groupIds, bool blocks = true, bool devices = true, bool features = true, bool scripts = true, bool subgroups = true, int subgroupDepth = 10)
        {
            var items = new List<LightwaveRfWebsocketMessageItem>();

            foreach (var groupId in groupIds)
            {
                items.Add(new LightwaveRfWebsocketMessageItem
                {
                    payload = new
                    {
                        groupId = groupId,
                        blocks = blocks,
                        devices = devices,
                        features = features,
                        scripts = scripts,
                        subgroups = subgroups,
                        subgroupDepth = subgroupDepth
                    }
                });
            }

            var response = await WebSocketSendMessageAsync(new LightwaveRfWebsocketMessage("group", "read") { items = items.ToArray() });

            foreach (var item in response.items)
            {
                if (!item.success.GetValueOrDefault())
                {
                    Console.WriteLine("Read Groups Failed");
                    return false;
                }
                else
                {
                    Console.WriteLine("Read Groups Succeeded");

                    foreach (var device in (item.payload.devices as JObject).Children().Select(child => child.First.ToObject<LightwaveRfDevice>()))
                    {
                        Console.WriteLine("Device: " + device.name);
                        _DEVICES[device.deviceId] = device;
                    }
                    
                    foreach (var feature in (item.payload.features as JObject).Children().Select(child => child.First.ToObject<LightwaveRfFeature>()))
                    {
                        _FEATURES[feature.featureId] = feature;
                    }
                }
            }

            return true;
        }

        public static async Task<bool> LightwaveRfLightwaveRfReadHierarchyAsync(string[] groupIds)
        {
            var items = new List<LightwaveRfWebsocketMessageItem>();

            foreach (var groupId in groupIds)
            {
                items.Add(new LightwaveRfWebsocketMessageItem
                {
                    payload = new
                    {
                        groupId = groupId
                    }
                });
            }

            var response = await WebSocketSendMessageAsync(new LightwaveRfWebsocketMessage("group", "hierarchy")
            {
                items = items.ToArray()
            });

            foreach (var item in response.items)
            {
                if (!item.success.GetValueOrDefault())
                {
                    Console.WriteLine("Read Hierarchy Failed");
                    return false;
                }
                else
                {
                    Console.WriteLine("Read Hierarchy Succeeded");
                    _FEATURESETS.Clear();

                    var featureSets = (item.payload.featureSet as JArray).Children();
                    foreach (dynamic featureSet in featureSets)
                    {
                        Console.WriteLine("Feature Set: " + featureSet.name);

                        foreach (var featureId in featureSet.features)
                        {
                            // We're only interested in storing the user specified names for device "features"
                            // But if we wanted to, this gives us the system hierarchies:
                            // root group > homes > zones > rooms > feature sets > device features
                            // root group > homes > favourites > feature sets > device features

                            // It seems the user specified names (i.e. bedroom switch 1 and bedroom switch 2 on a dual dimmer)
                            // are only accessible within the hierarchy and cannot be read via the main group read operation,
                            // despite a group update event containing the new name whenever it is updated.
                            _FEATURESETS[(string)featureId] = (string)featureSet.name;
                        }
                    }
                }
            }

            return true;
        }

        public static async Task<bool> LightwaveRfLightwaveRfReadFeaturesAsync(string featureId)
        {
            // The protocol/API appears to have a bug in feature reading and writing
            // where the response is broken up into individual items, none with a transaction ID
            // relating to the original request (which could be for one or more items)

            // Therefore we limit this method to reading one feature per call, and track its reponse
            // via the incremental item ID in the request

            var response = await WebSocketSendMessageAsync(new LightwaveRfWebsocketMessage("feature", "read") 
            {
                items = new[]
                {
                    new LightwaveRfWebsocketMessageItem
                    {
                        payload = new
                        {
                            featureId = featureId
                        }
                    }
                }
            });

            foreach (var item in response.items)
            {
                if (!item.success.GetValueOrDefault())
                {
                    Console.WriteLine("Read Feature Failed");
                    return false;
                }
                else
                {
                    Console.WriteLine("Read Feature Succeeded");

                    LightwaveRfFeature feature;
                    if (_FEATURES.TryGetValue(featureId, out feature))
                    {
                        feature.attributes.status = item.payload.status;
                        feature.attributes.value = item.payload.value;
                    }
                }
            }

            return true;
        }

        public static async Task<bool> LightwaveRfLightwaveRfWriteFeatureAsync(string featureId, int value)
        {
            // The protocol/API appears to have a bug in feature reading and writing
            // where the response is broken up into individual items, none with a transaction ID
            // relating to the original request (which could be for one or more items)

            var response = await WebSocketSendMessageAsync(new LightwaveRfWebsocketMessage("feature", "write")
            {
                items = new[]
                {
                    new LightwaveRfWebsocketMessageItem
                    {
                        payload = new
                        {
                            featureId = featureId,
                            value = value
                        }
                    }
                }
            });

            foreach (var item in response.items)
            {
                if (!item.success.GetValueOrDefault())
                {
                    Console.WriteLine("Write Feature Failed");
                    return false;
                }
                else
                {
                    Console.WriteLine("Write Feature Succeeded");
                }
            }

            return true;
        }
        
        public static void LightwaveRfEventHandler(LightwaveRfWebsocketMessage receivedMessage)
        {
            foreach (var item in receivedMessage.items)
            {
                LightwaveRfFeature feature = null;
                if (_FEATURES.TryGetValue((string)item.payload.featureId, out feature))
                {
                    feature.attributes.value = item.payload.value;
                    Console.WriteLine("EVENT: {0} Attribute: {1} Value: {2}", (_FEATURESETS[feature.featureId] ?? feature.attributes.name), feature.attributes.type, item.payload.value);
                }
            }
        }

        public static async Task<string> GetAccessTokenAsync(string email, string password)
        {
            var authentication = new
            {
                email = email,
                password = password,
                version = "1.6.6"
            };

            string authenticationJson = JsonConvert.SerializeObject(authentication);

            // Get Access Token
            using (var httpClient = new HttpClient())
            {
                var httpRequestMessage = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri("https://auth.lightwaverf.com/v2/lightwaverf/autouserlogin/lwapps")
                };

                httpRequestMessage.Headers.Add("x-lwrf-platform", "ios");
                httpRequestMessage.Headers.Add("x-lwrf-appid", "ios-01");

                var httpRequestContent = new StringContent(authenticationJson);
                httpRequestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                httpRequestMessage.Content = httpRequestContent;

                var result = await httpClient.SendAsync(httpRequestMessage);
                result.EnsureSuccessStatusCode();

                dynamic authenticationResult = JObject.Parse(result.Content.ReadAsStringAsync().Result);
                return authenticationResult.tokens.access_token;
            }
        }
    }
}
