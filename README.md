# LightwaveRF-LinkPlus (Generation 2)
A project to collate some thoughts and details on LightwaveRF LinkPlus (Generation 2).

# About
[LightwaveRF](https://lightwaverf.com) are a smart home product manufacturer, primarily retrofit light switches/dimmers and sockets for the UK market.

Their ecosystem is built around an Internet connected hub/bridge known as the Link or LinkPlus, which communicates with the smart things over a proprietary 868MHz RF protocol. Their newly released *Generation 2* products feature two-way RF communication, primarily allowing for the smart things to report back their statuses to the hub (otherwise it wouldn't know when the user had manually switched on a switch for example).

Generation 2 also brings support for integration with other ecosystems such as Apple HomeKit. However at this time there is no official API, so you're limited to using the official smartphone app, or one of the supported integrations. I wanted to do something a bit different (more on that below).

Generation 1 refers to their legacy products and ecosystem which are completely different (although the Gen 2 LinkPlus hub has some compatibility with legacy Gen 1 products).

# API
Until such a time an official API is published, it's fairly trivial with a bit of engineering to figure out and replicate the behavior of the official smartphone app.

The smartphone app communicates with the LightwaveRF "cloud" service via a persistent WebSocket (over SSL/TLS) at **wss://v1-linkplus-app.lightwaverf.com**. It's surprisingly responsive for a remote service/broker, however I do hope the official API (when it is released) interacts directly (therefore locally) with the hub to drop this dependency on Internet connectivity and 3rd party service.

### Known WebSocket Request Commands

**class** | **operation**
:--- | :---
user | authenticate
user | update
user | getConsentObject
user | getMarketingPreferences
user | rootGroups
user | addStructure
group | read
group | hierarchy
group | add
group | update
group | delete
user | link2
system | link
device | read
device | add
device | update
device | delete
feature | read
feature | write
feature | update
block | add
block | read
block | delete
block | update
script | add
script | trigger
script | update
script | read
script | delete
firmware | readForDevice
firmware | update

I won't detail every single WebSocket message command/type here, **[there is an example C# program in this project](https://github.com/washcroft/LightwaveRF-LinkPlus/blob/master/Program.cs)** which covers all the basic functionality you're likely to need. However, WebSocket messages typically take the following forms:

### Request

```
{
   "version":1,
   "senderId":"7e46aa52-c862-4556-9cb3-3b8f57e15254",
   "transactionId":425988,
   "direction":"request",
   "class":"feature",
   "operation":"read",
   "items":[
      {
         "itemId":52,
         "payload":{
            "featureId":"3b8f57e152543b8f57e15254-25-3b8f57e152+0"
         }
      }
   ]
}
```

### Response

```
{
   "version":1,
   "senderId":1,
   "transactionId":425988
   "direction":"response",
   "class":"feature",
   "operation":"read",
   "items":[
      {
         "itemId":52,
         "payload":{
            "value":100,
            "status":"ok"
         },
         "success":true
      }
   ],
}
```

In this case, we're reading the brightness (dimLevel) of a switch with ID `3b8f57e152543b8f57e15254-25-3b8f57e152+0`. The response is 100%.

You'll notice the `transactionId` field is the same in both request and response (and `itemId` for the inner items), this is how you know which response message relates to which request.

### Unsolicited Change Notification

```
{
   "version":1,
   "senderId":1,
   "transactionId":362354854,
   "direction":"notification",
   "class":"feature",
   "operation":"event",
   "source":"server",
   "items":[
      {
         "itemId":2,
         "payload":{
            "featureId":"3b8f57e152543b8f57e15254-25-3b8f57e152+0",
            "value":100,
            "status":"ok"
         }
      }
   ]
}
```

## Authentication

Obviously, any communication with the service needs to be authenticated. An access token can be obtained in exchange for your valid LightwaveRF account email/password credentials at [https://auth.lightwaverf.com/v2/lightwaverf/autouserlogin/lwapps](https://auth.lightwaverf.com/v2/lightwaverf/autouserlogin/lwapps).

### Request

```
POST https://auth.lightwaverf.com/v2/lightwaverf/autouserlogin/lwapps HTTP/1.1
Host: auth.lightwaverf.com
Content-Type: application/json
x-lwrf-platform: ios
x-lwrf-appid: ios-01
Accept: */*
Accept-Language: en-us
Accept-Encoding: br, gzip, deflate
User-Agent: LightwaveApp/107001 CFNetwork/901.1 Darwin/17.6.0

{
   "email":"[your email address]",
   "password":"[your password]",
   "version":"1.6.8"
}
```

### Response

```
HTTP/1.1 200 OK
Server: nginx/1.8.0
Date: Thu, 21 Jun 2018 12:29:53 GMT
Content-Type: application/json; charset=utf-8
Connection: keep-alive
Access-Control-Allow-Origin: *

{
   "user":{
      "givenName":"Warren",
      "familyName":"Ashcroft",
      "email":"[redacted]",
      "lightwaveRfPublic":{
         "country":"United Kingdom"
      },
      "providers":{

      },
      "_id":"[redacted]",
      "created":1507678000000,
      "modified":1507678000000
   },
   "tokens":{
      "access_token":"[redacted]",
      "token_type":"Bearer",
      "expires_in":604800,
      "refresh_token":"[redacted]",
      "id_token":"[redacted]"
   }
}
```

### WebSocket Messages

```
{
   "version":1,
   "senderId":"[generate a fixed GUID for the session]",
   "transactionId":1,
   "direction":"request",
   "class":"user",
   "operation":"authenticate",
   "items":[
      {
         "itemId":0,
         "payload":{
            "token":"[your access_token]",
            "clientDeviceId":"[generate a random GUID for the authentication]"
         }
      }
   ]
}

{
   "version":1,
   "senderId":1,
   "transactionId":1,
   "direction":"response",
   "class":"user",
   "operation":"authenticate",
   "items":[
      {
         "itemId":0,
         "success":true,
         "payload":{
            "handlerId":"[redacted - unused]"
         }
      }
   ]
}
```

It's likely these access tokens will expire and can be refreshed (without credentials) with another request. I haven't encountered this yet, but again it would be fairly trivial to incorporate.

# Something A Bit Different...

I mentioned above I needed an API to do something a bit different.

In my home, I have a mix of smart bulbs (Philips Hue) and smart switches (LightwaveRF). Smart bulbs need to be constantly powered (even when "off", to receive "on" commands), and smart switches are designed to sit inline with non-smart bulbs.

If you try and put a smart bulb and *any* smart/dumb switch on the same circuit, the bulb won't receive the constant power it needs when the switch is off, or worse with a smart switch, the switch and/or bulb would likely be damaged - due to the way smart switches are effectively computer controlled dimmers designed to operate non-smart bulbs.

**Normally, you would use either a smart bulb or a smart switch on any single circuit, not both...**

With a smart bulb, to avoid navigating your smartphone for the required controls in person - you would cover over the original dumb switch on the wall (leave permanently switched/wired on), and fix some other ugly non-matching battery powered switch designed specifically for your smart bulb ecosystem where the old switch used to be to control the bulb.

With a smart switch, they're a direct retrofit replacement for the old dumb switch on the wall, giving your non-smart light fixtures new smarts.

If you wanted to advantages of both, you'd be out of luck. Why would you want/need both?

![Smart Switch](https://github.com/washcroft/LightwaveRF-LinkPlus/raw/master/reference/smartswitch.jpg "Smart Switch")

**Smart switch advantages:**
* Constantly mains powered, no need for batteries
* Permanent status LEDs, illuminating the switch in the dark
* Good design and usability, matching existing wall accessories
* Direct replacement for the dumb switch compatible with its back box, doesn't look out of place

![Smart Bulb](https://github.com/washcroft/LightwaveRF-LinkPlus/raw/master/reference/smartbulb.jpg "Smart Bulb")

**Smart bulb advantages:**
* Additional functionality, can emit different colours and shades
* Well adopted, powerful APIs and lots of integration opportunities
* Capable of creating immersive experiences and dramatic lighting effects

### How can we have both?

There are two main problems to solve:

#### 1) Prevent damage to the equipment when both smart switch and smart bulb coexist with one another

**How Smart Switches (or Dimmers) Work**

As mentioned above, smart switches are (usually) designed to sit inline (in series) with bulbs. That means **live** is connected to the switch, from the switch to the bulb, and then from the bulb to **neutral**. You'll notice there is no neutral for the switch, so how is the switch itself powered?

Smart switches are effectively computer controlled dimmers, they have the ability to limit the amount of current flowing through the circuit to control the amount of light emitted by the bulb. These dimmers always allow a tiny amount of current to flow through the complete circuit, enough for the switch's own control circuitry but not enough for any light to be emitted by the bulb (unless your bulbs are LEDs, in which case the tiny amount of current might be enough to make them glow slightly!). When the bulb should be emitting light, it simply allows more current through.

There's a bit more to it than that, but that's enough to understand what's going on.

**How Switches Are Wired**

In a typically wired UK home, **live** and **neutral** on the main lighting ring/radial supply would be taken to the ceiling rose or light fixture. From there a separate *switch wire* would be taken to its wall switch - usually the same 1.5mm "twin and earth" type cable is used, but the blue conductor is sleeved brown to indicate this isn't **neutral** but **switched live**. The wires are connected in such a way so that the supply runs through the switch before the bulb.

**Modifying the Smart Switch and Wiring**

Obviously, if you've read this far I expect you understand what you're doing, why you're doing it and the effects of it. You may need to deal with unusual wiring arrangements, such as those involving multiple light fixtures or where intermediary wall switches are involved. Your mileage may vary and you are responsible for your own actions! Be safe!

Both smart switch and smart bulb need a permanent supply, therefore take the incoming **live** and **neutral** supply at the ceiling rose or light fixture and connect them to both the bulb and the switch - for the switch via what was the switch wire (i.e. the **switched live** becomes the **neutral** conductor).

Now there is a potential for a dead short from **live** to **neutral** through the smart switch, as it'll ramp up the amount of current flowing through it when it is asked to switch on the bulb (normally a traditional bulb's resistance acts as the current limiter, but directly connected to L and N like this there is no other load involved).

To fix this, the smart switch needs to be modified so that even if it's control circuity wants to allow more current through, it never actually opens the taps, preventing the dead short scenario.

__LightwaveRF 1/2 Gang__

In the case of the LightwaveRF 1/2 gang smart switches, they are a classic "back-to-back MOSFET" dimmer design, where the sophisticated control circuitry simultaneously drive the gates of [IPA70R360P7S N-Channel Power MOSFET](https://www.infineon.com/dgdl/Infineon-IPA70R360P7S-DS-v02_00-EN.pdf?)s at each terminal, opening a conductivity between them. Simply by disconnecting these gates from the control circuitry, the MOSFETs can never conduct and therefore preventing the dead short scenario.

[![LightwaveRF L21 Control PCB Front](https://github.com/washcroft/LightwaveRF-LinkPlus/raw/master/reference/lightwaverf_l21_control_front.thumb.jpg "LightwaveRF L21 Control PCB Front")][lightwaverf_l21_control_front] [![LightwaveRF L21 Control PCB Back](https://github.com/washcroft/LightwaveRF-LinkPlus/raw/master/reference/lightwaverf_l21_control_back.thumb.jpg "LightwaveRF L21 Control PCB Back")][lightwaverf_l21_control_back]

[lightwaverf_l21_control_front]: https://github.com/washcroft/LightwaveRF-LinkPlus/raw/master/reference/lightwaverf_l21_control_front.jpg
[lightwaverf_l21_control_back]: https://github.com/washcroft/LightwaveRF-LinkPlus/raw/master/reference/lightwaverf_l21_control_back.jpg

[![LightwaveRF L21 Power PCB Front](https://github.com/washcroft/LightwaveRF-LinkPlus/raw/master/reference/lightwaverf_l21_power_front.thumb.jpg "LightwaveRF L21 Power PCB Front")][lightwaverf_l21_power_front] [![LightwaveRF L21 Power PCB Back](https://github.com/washcroft/LightwaveRF-LinkPlus/raw/master/reference/lightwaverf_l21_power_back.thumb.jpg "LightwaveRF L21 Power PCB Back")][lightwaverf_l21_power_back]

[lightwaverf_l21_power_front]: https://github.com/washcroft/LightwaveRF-LinkPlus/raw/master/reference/lightwaverf_l21_power_front.jpg
[lightwaverf_l21_power_back]: https://github.com/washcroft/LightwaveRF-LinkPlus/raw/master/reference/lightwaverf_l21_power_back.jpg

The pins circled in red connect the MOSFET gates to the control circuity, these pins can simply be bent flat to facilitate their disconnection, allowing easy modification reversal in the future.

__LightwaveRF 3/4 Gang__

Like the LightwaveRF 1/2 gang smart switches, the 3/4 gang smart switches are a similar design, however they accept an **optional neutral connection**. This means no modifications are necessary, simply connect live and neutral, and leave all four switched outputs disconnected (or use them as normal if they are not smart bulb circuits, you can even mix and match across all 4 gangs).

**Result**

* A smart bulb with a permanent power supply, no loss of "reachability"
* A smart switch with a permanent power supply, that thinks and behaves as if controlling lighting, but now is actually a glorified user interface/input device

#### 2) Syncronising the state of the switch with the bulb and vice-versa

This is fairly straight forward, so long as both your smart switches and smart bulbs have an API. Since I'm using LightwaveRF switches and Philips Hue bulbs, thanks to the above API work, and the Philips Hue API, they do.

A simple program would run continuously (on a Pi or similar) to monitor both switches and bulbs. When a switch is turned on, the corresponding bulb is sent a command to turn on... When a bulb is dimmed to 50%, the corresponding switch is sent a command to dim to 50%... Any good developer will have already identified the need to prevent loops here, but you get the idea, straight forward stuff to complete the setup.

Is it worth it? I think so, others might be more accepting of unsightly battery powered additions:

![Battery Powered Smart Switch](https://github.com/washcroft/LightwaveRF-LinkPlus/raw/master/reference/batteryswitch.jpg "Battery Powered Smart Switch")


# License
```
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
```