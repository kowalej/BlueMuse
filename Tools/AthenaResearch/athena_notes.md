# Athena Notes

## Data Service (UUID: `0000fe8d-0000-1000-8000-00805f9b34fb`)

### Device Control / Info Characteristic (UUID: `273e0014-4c4d-454d-96be-f03bac821358`)

- Writes seem to always be to this handle.
- Data receive notifications seem to contain JSON data with device information, or simple ("rc": 0 - ACK?).

#### Old Commands - Worked on Muse 2016, Muse 2, Muse S, etc.

- Device Info: "v1"
- Control Status: "s"
- Stream Start: "d"
- Stream Stop: "h"
- Stream Keep Alive: "k"
- Device Reset: "*1"

#### Command `0376340a` ("v4")

Seems to be the equivalent of the old "v1" command.

Observed this response right after (decoded ASCII from 8 x 32 byte chunks):

```json
{
  "fw":"3.1.11",
  "bn":1,
  "tp":"consumer",
  "hw":"01.0",
  "pv":4,
  "ap":"headset",
  "sp":"Athena_RevE",
  "hb":"Athena_RevE",
  "bl":"1.0.1",
  "be":"1.5. 1",
  "rc":0
}
```

#### Command `0664633030310a` ("dc001")

Appears to be involved in starting a stream. May have to send twice or send "d then "dc001".

Observed this response right after (decoded ASCII from 1 32 byte chunk).

```json
{
  "rc":0
}
```

#### Command `02680a`("h")

Assumed this is the same as before and will stop the data stream.

Observed this response right after (decoded ASCII from 1 32 byte chunk).

```json
{
  "rc":0
}
```

#### Command `02730a` ("s")

Seems to be the same as old status command.

Observed this response right after (decoded ASCII from 8 x 32 byte chunks) - note: the MC (MAC address) portion is redacted for privacy.

```json
{
  "hn": "MuseS-F723",
  "sn": "7010-T3NC-F723",
  "ma":"REDACTED",
  "hs":"DC21-2FX2-3025",
  "id":"0",
  "bp":100.00,
  "ps":4165,
  "ln":"0",
  "rc":0
}
```

#### Command `047032310a` ("p21")

This used to the default model - "p21" would stream EEG.

#### Command `0670313034350a` ("p1045"):

Seems like a new mode? Right after sending this, an "s" - status command was sent, and received:

```json
{
  "hn":"MuseS-F723",
  "sn":"7010-T3NC-F723",
  "ma":"REDACTED",
  "hs":"DC21-2FX2-3025",
  "id":"0",
  "bp":99.68,
  "ps":4165,
  "ln":"0",
  "rc":0
}
```

Slight change in bp - perhaps corresponds to power mode?


### Primary Data Streaming Characteristic (UUID: `273e0013-4c4d-454d-96be-f03bac821358`)

- Seems to at least provide the EEG data - not sure about the fNIRS, gryo, accel, etc.

### Presumed Other Data Streaming Characteristic (UUID: `273e0001-4c4d-454d-96be-f03bac821358`)

- Weirdly this is same characteristic UUID for old Muse device control / info.

## Binary Encoding Format

### Sensor Data Assumptions

#### EEG
9 EEG channels (TP9, AF7, AF8, TP10, ref: FPz + 4 AUX) @ 256Hz
Sample size: @ 14 bits / channel, we get 126 bits totals = **15 bytes**

#### PPG
3 wavelengths (per side) (IR (850nm), Near-IR (730nm), Red (660nm)) @ 64Hz
Sample size: @ 20 bits / wavelength, we get 120 bits total = **15 bytes**

#### fNIRS Sensor
5-optode (per side) bilateral frontal cortex hemodynamics @ 64Hz
Sample size: @ 20 bits / optode = 200 bits total = **25 bytes**

#### Accelerometer
Three-axis (X,Y,Z) - 52Hz
Sample size: @16 bits / axis = 48 bits total = **6 bytes**

#### Gyroscope
Three-axis, @52Hz\
Sample size: @16 bits / axis = 48 bits total = **6 bytes**

Total "services": 5
Total data points: 9 + 3 + 5 + 3 + 3 = 23

#### Sample Packet Values (aka "raw values")

###### Sample 1

Hex
```hex
                                    e3 fc 02 4e
48 e1 89 93 01 47 e4 d2 02 00 e7 13 81 00 64 3e
74 00 1c fd 56 00 b8 12 94 ff 1c 3e 84 00 a9 fc
0e 00 b7 11 6a fe c5 3b b1 00 09 fd ac fe 34 6e
7b 24 00 08 35 d3 e9 37 3d 06 f2 ac 20 d8 34 43
e9 37 56 06 52 ae 20 2e 35 03 ef 37 54 06 c2 af
20 47 e5 53 72 00 4a 12 c4 00 76 3b da 00 c1 fe
5f fd f3 12 72 03 1f 3e 0b 01 41 ff 42 fe 95 12
02 03 a9 3d 37 01 fe fe cc ff 34 6f 8d e6 00 40
35 83 ef 37 85 06 32 b4 20 63 35 03 f2 37 a4 06
92 b7 20 1d 35 83 ef 37 af 06 72 b7 20 12 a0 a0
a8 01 ff bf 94 38 14 ae dd 5f 22 8f 08 1d f6 e3
a0 f7 db 87 e6 bd d7 fe a1 75 d8 14 9a dc 12 a1
dd a8 01 d1 75 e6 a7 de c1 d7 ac e1 5d 88 0c 4e
d4 57 bf 8f f8 0d 76 d6 95 61 64 48 0c 96 d2
```

Binary
```bin
                                    11011001 00000011 00000011 01100100 
01001001 11100001 10001001 10010011 00000001 01000111 11101001 00001100 
00000010 00000000 11010101 00010010 10011010 00000000 10100100 00111111
11001101 11111111 11100000 11111101 10010111 11111111 10010100 00010010
11110100 00000000 00110011 00111110 01011101 00000000 00001001 11111101
11111110 11111110 10001010 00010010 11100101 00000001 11000001 00111101
11101110 00000000 00100110 11111101 11001010 11111110 00110100 01110100
11111011 00100110 00000000 01000001 00111010 10110011 01001010 00111000
10000000 00001010 11100010 11111010 00100000 01000011 00111010 10100011
01001100 00111000 10110111 00001010 10100010 00000000 00100001 01010011
00111010 10010011 01010010 00111000 11101011 00001010 11100010 00000100
00100001 00010010 11000000 10100100 01011101 00000001 11111111 00111111
00001111 10011000 00010001 00000010 01110110 00100110 11100001 01110111
00101000 00100111 11011010 11110110 00110111 11111110 01100001 11100111
11010101 10001001 01110001 10011101 00100000 01010010 01101000 00011011
01001110 11101111 00010010 11000001 11100001 01011101 00000001 11111111
11111111 00000010 01000111 11001111 00010101 01111111 11000101 00100000
01010000 01011000 00011000 10001010 11101010 11111111 01111111 10110000
00010111 11110000 01011001 10001110 00000010 10100001 01010011 00111000
00011001 01011110 11100010 00010010 11000010 00011110 01011110 00000001
11111111 10111111 01000100 11101000 00000101 00011110 10010011 01100101
11100001 01100110 11101000 00011000 00100010 11100100 11111111 11111111
11001010 11100111 11100100 11100101 10001101 10000000 00100010 10111110
10101000 00100101 11100010 11100010 00010010 11000011 00111100 01011110
00000001 11111111 01111111 00111010 10110111 10111101 11010001 10010000
01000110 01010111 10010110 10010101 10100111 00100101 01101111 11111111
00111111 10110011 11100111 11011101 10011101 10100000 01011001 10010111
10010010 10000101 10010001 00001101 10011011
```

The following will be in zero index format (meaning bit 0 is the first bit).

##### Bits 0 - 8 (Byte 00)
  - == 227 (decimal)
  - indicates total number of bytes

##### Bits 8 - 16 (Byte 01)
  - == 

##### Bits 48 - 64 (Bytes 06, 07, 08)

##### Bits 72 - 80 (Byte 09)

Possible values are 18, 71, 52, 83.
    _pfe a
18: 0001 0010
71: 0100 0111
52: 0011 0100
83: 0101 0011

18:
  - 221
  - 248
  - 252

71:
  - 237
  - 
