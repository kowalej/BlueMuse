from bitstring import BitArray
import struct
import json

def bytes_to_bitarray(byte_array):
    c = BitArray()
    c.append(BitArray(hex=byte_array))
    return c.bin


def bin_to_dec(bin):
    return int(bin, 2)

def bin_to_float(bin):
    return struct.unpack('!f',struct.pack('!I', bin_to_dec(bin)))[0]

b_1321 = 'e3fc024e48e189930147e4d20200e7138100643e74001cfd5600b81294ff1c3e8400a9fc0e00b7116afec53bb10009fdacfe346e7b24000835d3e9373d06f2ac20d83443e937560652ae202e3503ef375406c2af2047e55372004a12c400763bda00c1fe5ffdf31272031f3e0b0141ff42fe95120203a93d3701fefeccff346f8de600403583ef37850632b420633503f237a40692b7201d3583ef37af0672b72012a0a0a801ffbf943814aedd5f228f081df6e3a0f7db87e6bdd7fea175d8149adc12a1dda801d175e6a7dec1d7ace15d880c4ed457bf8ff80d76d6956164480c96d2'
ba_1321 = bytes_to_bitarray(
    b_1321
)

b_1322 = 'ecfd02bb48e189930112a2240200ff7fba1819e2d07ca168b80d86d09e3cfe67e53dcf6ae15f6809d6cb5390be26002f2041202c203f202e2040202f203f202e203e202c20412012a3be260074fb2537acf9c1b0a045180622c2dd7fc6a7cbd1c00be2c9681a82d612a41a2700ff3f9ac80ae6c639a194f81be6b8473dc3881b96d284938364858d4f12a5572700a4b73e280dcecbe1d8d9059f6daf087d89181802c0eae28af81152fd12a6752700ff3f09692f9ebc9ca143781c76ef72bd86b8fb59b83e22a6282e92f312a7b22700b569dcf7d315b6caa17d581f7ae72cdf282801aeb5b8e160d81022dc'
ba_1322 = bytes_to_bitarray(
    b_1322
)

b_1323 ='f0fe02c548e189930112a803030067a9beb81ba2afc6e16ef8135ed9ff2bb438167aa98ba17ec81a6ed812a93d0000a997f227e2b5a52ba15c980c4ace6c97f447ebd9a4d32035b8fe15c012aa7a0000d8717bb80b0ea5b36187780ddacfff7f9f28ffada20de2ae781c3eca12abb700004e3910a8da55a080966155995960dd6de687df2da23b9916c69f0da212acf4000041ee5358f99da3b122c0e81b8af9f9e89ca80fbaa09ea1738820fae712ad130100449427a8f02198ca2185981b72e3d542d6f7c5419d7c216b580ed6d612ae5001002c450428e8719e57a15788050ed0f94748d81b9e948ce169080b22ce'
ba_1323 = bytes_to_bitarray(
    b_1323
)

b_1324 ='e3ff02c648e189930112afa8000066411f0801c2944a21661807e2c80040cb07c3e99c7ce032e8f885bd34706aafff303523f137990672b520583593f137b80602b720a63503f637fa0682ba2047e6c32300f2120a026b3d6700bffe120166131701e03b8aff99ff440242137400613e45ff5100bb023471ba7100ff35a3fc375607b2c0207c363306389207a2c520e736430e38f107a2cd2047e77898001c132800743d2efff4ffc9029813cefe313e5fff52002002b313cdfe313f22003c001f0112b021cf0163c0e0f7de399edce04388f6bdc300004cd8157a998421796801b2b9' 
ba_1324 = bytes_to_bitarray(
    b_1324
)

b_1325 = 'f000034649e189930112b1c402000e405d481b72964f965a158dd1570e80f717058e988698e8c593d59712b27a00000dc0bfa7f1f5970c2294e80afeed0e4003f8f93d94e0e047580fdedd12b39900000e4044480aa6958e61771815b2df0ec02608f7a19bd12182981616dc12b4d600000e00c1b7dcf1989c61726813eed70e00ebc7ee8d9257a151380c96d212b51301000ec039380cda8b606146080602ce0e803c98114290bc2032f8026ac512b65001000e40ca27e1e990ede046880422ca0ec0aac7ea5589e02180080efac312b76e01000e00271828c686ec163e958ebd5e0e005fb8364e85485888a587d995'
ba_1325 = bytes_to_bitarray(
    b_1325
)

b_1326 = "ec01034749e189930112b88700000ec0b5f7011280cee16ed80bf6f20d407237ee7978c22048f81a2ae512b95c00000ec0d5670f4a7234615dc81756e50ec05c982da67ba7e18ce82206e312ba7a00000e40ead702aa7e86218e8824f6de0ec064a7e9697bf8e05f881656d812bbb700000e0098b7032e76d0a05d5812aad30e003678237273aa6060b81242cc12bcf400000e80f257179e6b54204bb809facd0e404c57eb8565052180580dfac753913101002e2041202c2042202d2040202f2042202f2045202a20432012bd3101000ac074c7ed096f04d64f0597796000001a98124e7405978e9594f995"
ba_1326 = bytes_to_bitarray(
    b_1326
)

b_1327 = 'd302034749e189930112 bef50100fcc112680b7e707aa197b81a22f9000079b7f6b979a0e04c481f8ee912bf01240097cc4ad7fd457e8da03cd81356e92fb9e6f720c27f2c616f7821f6f23472b63aff8237131838650892d5200538132238c208b2db209d38c32b382209a2e120981c824a002b640d010d013e1affff81014e7054cd89ff781f47e829d6ff7c139cff8140eb0075ff18006a134900ab3e3a0063feaeff341395ff653e70ff44feccff34735c2400e9383334387b0952e8204e39b33b38ed0992ef20c939134338400ab2f520'
ba_1327 = bytes_to_bitarray(
    b_1327
)

b_1328 = 'd903036449e189930147e90c0200d5129a00a43fcdffe0fd97ff9412f400333e5d0009fdfefe8a12e501c13dee0026fdcafe3474fb2600413ab34a38800ae2fa20433aa34c38b70aa20021533a935238eb0ae2042112c0a45d01ff3f0f9811027626e1772827daf637fe61e7d589719d2052681b4eef12c1e15d01ffff0247cf157fc5205058188aeaff7fb017f0598e02a15338195ee212c21e5e01ffbf44e8051e9365e166e81822e4ffffcae7e4e58d8022bea825e2e212c33c5e01ff7f3ab7bdd19046579695a7256fff3fb3e7dd9da059979285910d9b'
ba_1328 = bytes_to_bitarray(
    b_1328
)

b_3639 = 'ecd70ae973e289930112203f0200e4202598137e8449616a880efed4bba13ff81b62852fe15a380e1ecb12217a000053a23c980b128448a2aa38168ad98a611408069e8150201fb80272b31222b70000636004380b368367d50b1585a157b6a021480e8685411ba4e6b5adba1223dc26007a21409802aa83e362c7f82492fa3ca10818084282af61741825bee9530cdc26002c2047202d2045202e2047202c2047202d2047202e2046201224382700df9ff5a705e282e8a186b820eee99b9f3e280a6685c9e178481b42e41225752700a6e047f821528403e285481ebee0d8a022080d3282bae17da81cd6db'
ba_3639 = bytes_to_bitarray(
    b_3639
)

b_5323 = 'f0dd103159e389930112a5090000c19e32e8fb3185bf6272293afeff9c1e09a8f7358344a111393722f812a67a000009a0035816b28380e10f892ffeffb0a00f481d228557a100192c9eff12a7b7000080dffb77f881834aa113493082f9f9deee97f09582062100792702f112a8f400008f2023c8f8858520e1de881ac2e93be131e80c3a862b61ca38139ae312a9310100c61f0cd80d468290e0c808120edf669ee7e7f83180a460e218174edb12aa500100375ff6a710c28210e1eee816bed64fe0023823228459e0b71811dece12ab8d0100829ffcf7131a83b3e26dd92edee2e39df5e7fa2582f35a3ab7dc7d89'
ba_5323 = bytes_to_bitarray(
    b_5323
)

ba_other = 'df3300e5350a8a93013551a80300aed225425fddcde0940bf91575ea488264d0d4046ad2053e5fbecd60930b9b1595e3487c6430d50411a0117500e262114805e6910edd0c48feed1e9c5b04d8fdf54628a0fc47fcb5be11a14e75007424fd47000ed9a5241568055e95ed9e1c6801121ff85afd27fac138472b24d9ff02fa381c5d38fb00daffa4ffcbf9871c2039ab00c5ffcbffecf9bd1c61388f006efff5ff530c0c9c001e202e201d202e201e202f201b202f201f202c201f202e2011a2499c00f39deb77fc4db955a2f5d703ced656e2fd17052e9ad01ef1c7fdfd27'

some_hex_correlate = 'e5800253d0d1899301342622010035a8807a09d89fa00a0958a8c07b09dc9f200a095aa8507c09d49fc00b09477ff2740086045dff5f3f70003000e5ff380445ff0a3f71004000e2ff9c0444ff623f6d004b00e8ff342709ec0081a8c07f09da9fa00a0971a8808009dc9f100a098ea850810904a0e00a0934282c86017da8207f0916a0800b099ba860800919a0200c0983a8b07f09fa9f000b09478009ad01c6046aff693f68003e00eeff9b04fdfe523f61005200e3ff9e042cff423f6b002f00e2ff11700b97028b1900d0c8adc30080d2f7ff8b03eee7ff8f898610ffff7e0e0098cf'
shx_ba = bytes_to_bitarray(some_hex_correlate)
print("fish")
print(shx_ba)

packets = []
# Open the movement_capture_20250826_175814.json file.
with open('movement_capture_20250826_175814.json', 'r') as f:
    json_data = json.load(f)
    packets = json_data.get("packets", [])

# Iterate the 

# timestamps = [p.get("timestamp", 0) for p in packets]
# packets_raw = []
# for p in packets:
#     hex = p.get("hex", "")
#     timestamp = p.get("timestamp", 0)
#     timestamps.append(timestamp)
#     packets_raw.append(bytes_to_bitarray(hex))

# # Take a second worth of data.
# first_second = []
# for p in packets:
#     timestamp = p.get("timestamp", 0)
#     if timestamp > 1:
#         break
#     first_second.append(p.get("hex"))

# print(len(first_second))
# # Total bytes
# total_bytes = sum(len(bytes.fromhex(hex)) for hex in first_second)
# print(total_bytes)

# # Print diffs in timestamps.
# diffs = []
# for i in range(1, len(timestamps)):
#     diff = float(timestamps[i]) - float(timestamps[i - 1])
#     if diff > 0:
#         diffs.append(diff)
#     print(f"Timestamp diff {i}: {diff}")

# average_diff = sum(diffs) / len(diffs) if diffs else 0
# print(f"Average timestamp diff: {average_diff}")

def print_bin_8_x_8_grid(bin):
  output = ""
  bit_groups = []
  for i in range(0, len(bin), 8):
    bit_groups.append(bin[i:i + 8])
  # Pad 4 blank groups.
  output += " " * 8 * 4
  output += " " * 4
  # Print first 4 on this row.
  for b in bit_groups[0:4]:
      output += (b + " ")
  print(output)

  # Print rows of 8.
  for i in range(4, len(bit_groups), 8):
      output = ""
      for j in range(8):
          if i + j == len(bit_groups):
              break
          output += (bit_groups[i + j] + " ")
      print(output)

print_bin_8_x_8_grid(ba_1327)
print_bin_8_x_8_grid(ba_1328)

def byte_index(position):
    return (position - 1) * 8

masks = []
lengths = []

for b in [b_1321, b_1322, b_1323, b_1324, b_1325, b_1326, b_3639, b_5323]:
    bytes = bytes.fromhex(b)
    bytes_5_4_3 = bytes[5:2:-1]
    print(f"Bytes 5,4,3: {bytes_5_4_3}")
    bits = bytes_to_bitarray(bytes_5_4_3.hex())
    print(f" 12-bit values: {bin_to_dec(bits[0:20])}, {bin_to_dec(bits[20:24])}")

for p in packets[:30]:
    hex = p.get("hex", "")
    bytes = bytes.fromhex(hex)
    ba = bytes_to_bitarray(hex)

    # Byte 0 indicates byte count of packet.
    decoded_len = bin_to_dec(ba[0:8])

    # This validates length theory.
    if len(ba) / 8 != decoded_len:
        print(f"  !!! MISMATCH !!!")

    # Byte 01 seems to be a counter, which loops after 255.
    # It seems to reliably reset to zero at the start of a stream.
    byte_01_decoded_counter = bin_to_dec(ba[8:16])

    # Byte 02 seems to be another counter, which increments after byte 01 resets.
    # It seems to reliably reset to zero at the start of a stream.
    byte_02_decoded_counter = bin_to_dec(ba[16:24])

    byte_03_rando = bin_to_dec(ba[24:32])

    byte_09_value = bin_to_dec(ba[9 * 8:10 * 8])
    '10001001 1001001100000001'
    print(f"Len: {decoded_len}, Count: {byte_01_decoded_counter}, Secondary Count: {byte_02_decoded_counter}, Tertiary Count: {byte_03_rando}")
    print(f"Byte 09: {byte_09_value}")
    print(f"Byte 09 binary: {ba[9 * 8:10 * 8]}")
    print(f"Bytes 09-10: {ba[9 * 8:11 * 8]}")
    mask = ba[9 * 8:11 * 8]
    masks.append(mask)
    lengths.append(decoded_len)

    byte_04_counter = ba[32:40]
    byte_05_counter = ba[40:48]
    print(f"Other Counter: {bin_to_dec(byte_04_counter)} Other Counter Secondary : {bin_to_dec(byte_05_counter)}")

    bytes_5_4_3 = bytes[5:2:-1]
    print(f"Bytes 5,4,3: {bytes_5_4_3}")
    bits = bytes_to_bitarray(bytes_5_4_3.hex())
    print(f" 12-bit values: {bin_to_dec(bits[0:24])}, {bin_to_dec(bits[20:24])}")
    print(p.get("timestamp"))


    # Byte 03 seems to be a slow moving counter which increments over ~7 frames. Loops after 255.
    # Seemed to start at an arbitrary number at beginning of streams.

    # Byte 05 varied between 224, 225, and 209, with a stable value over long windows.

    # Bytes 06, 07, 08 always seem to be 137, 147, 1 respectively.
    print(f"Bytes 06, 07, 08: {bin_to_dec(ba[6 * 8:7 * 8])}, {bin_to_dec(ba[7 * 8:8 * 8])}, {bin_to_dec(ba[8 * 8:9 * 8])}")

    # Bytes 09, 010, 011 seem to jump randomly.

    # Byte 012 seems to always equal either 0, 1, 2, or 3. It seems to alternative pretty regularily from packet to packet.
    indicator = bin_to_dec(ba[12 * 8:12 * 8 + 8])
    print(f"Indicator: {indicator}")
    print()

# print("Masks: ")
# for i in range(len(masks)):
#     print(f"{lengths[i]}: {masks[i][:8]} {masks[i][8:]}")

# # Epoch rollover times (this is when byte 2 is back from 255 to 0, and byte 3 goes up by one).
# rollover_times = [1755698576.865783000,
#          1755698586.553445000,
#          1755698596.303116000,
#          1755698606.040360000
#         ]
# deltas = [j - i for i, j in zip(rollover_times[:-1], rollover_times[1:])]
# print(deltas)
