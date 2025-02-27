﻿using System;
using System.Diagnostics;

namespace UDP_Server.Models
{
    public class Parser
    {
        public FlightControlField Parse(byte[] data)
        {
            try
            {
                ByteStream stream = new ByteStream(data, 0, data.Length);
                CheckDataCondition(data);
                FlightControlField field = new FlightControlField
                {
                    // [Byte #5.]
                    // 7    번째 비트(MSB)를 추출
                    ModeOverride = stream.GetBits(4, 7, 1),
                    // 6 ~ 5번째 비트를 추출
                    FlightMode = stream.GetBits(4, 5, 2),
                    // 4 ~ 1번째 비트를 추출
                    ModeEngage = stream.GetBits(4, 1, 4),

                    // [Byte #6.]
                    // 7    번째 비트(MSB)를 추출
                    FlapOverride = stream.GetBits(5, 7, 1),
                    // 6 ~ 1번째 비트를 추출
                    FlapAngle = stream.GetBits(5, 1, 6),

                    // [Byte #7.]
                    // 7    번째 비트(MSB)를 추출
                    WingTiltOverride = stream.GetBits(6, 7, 1),
                    // 6 ~ 0번째 비트(LSB)를 추출
                    TiltAngle = stream.GetBits(6, 1, 7),

                    // [Byte #8.]
                    // 8  바이트를 추출
                    KnobSpeed = stream.Get(7),
                    // [Byte #9.]
                    // 9  바이트를 추출
                    KnobAltitude = stream.Get(8),
                    // [Byte #10.]
                    // 10 바이트를 추출
                    KnobHeading = stream.Get(9),

                    // [Byte #11.]
                    // 11 바이트를 추출
                    StickThrottle = stream.Get(10),
                    // [Byte #12.]
                    // 12 바이트를 추출
                    StickRoll = stream.Get(11),
                    // [Byte #13.]
                    // 13 바이트를 추출
                    StickPitch = stream.Get(12),
                    // [Byte #14.]
                    // 14 바이트를 추출
                    StickYaw = stream.Get(13),

                    // [Byte #15.~ Byte #18.]
                    // 15 ~ 18바이트까지 추출
                    LonOfLP = stream.GetBytes(14, 4),

                    // [Byte #19.~ Byte #22.]
                    // 19 ~ 22바이트까지 추출
                    LatOfLP = stream.GetBytes(18, 4),

                    // [Byte #23.~ Byte #24.]
                    // 23 ~ 24바이트까지 추출
                    AltOfLP = stream.GetBytes(22, 2),

                    // [Byte #25.]
                    // 7    번째 비트(MSB)를 추출
                    EngineStartStop = stream.GetBits(24, 7, 1),
                    // 0    번째 비트(LSB)를 추출
                    RaftDrop = stream.GetBits(24, 0, 1)
                };
                return field;
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine($"ArgumentException: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"General Exception: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// [데이터 파싱 조건]
        /// </summary>
        /// <param name="check"></param>
        private void CheckDataCondition(byte[] check)
        {
            // [# CRC 계산]
            byte[] crcData = new byte[26]; // [Byte #5.~ Byte #30.]
            Array.Copy(check, 4, crcData, 0, 26);

            // [Crc16ccitt] 계산 값
            ushort calculatedCrc = Crc16_ccitt.Crc16ccitt(ref crcData, (uint)crcData.Length);
            // [check]에서 받은 CRC 값 (31, 32 바이트 추출)
            ushort receiveingCrc = (ushort)((check[30] << 8) | check[31]);

            // [# CRC 비교]
            if (calculatedCrc != receiveingCrc)
            {
                Debug.WriteLine("해당 CRC 값을 다시 확인하세요.", "통신 실패");
                throw new ArgumentException("Mismatch CRC value!");
            }

            // 모든 바이트 길이가 32바이트 넘어가는지 확인
            if (check.Length > 32)
            {
                Debug.WriteLine("바이트 길이를 다시 확인하세요.", "통신 실패");
                throw new ArgumentException("Invalid data length");
            }

            // 첫 번째 바이트가 0xAF (Frame Sync)인지 확인
            if (check[0] != 0xAF)
            {
                Debug.WriteLine("프레임 싱크를 다시 확인하세요.", "통신 실패");
                throw new ArgumentException("Invalid Frame Sync!");
            }

            // 두 번째 바이트가 0x01(목적지 주소)인지 확인
            if (check[1] != 0x0A)
            {
                Debug.WriteLine("목적지 주소를 다시 확인하세요.", "통신 실패");
                throw new ArgumentException("Invalid Destination Address");
            }

            // 세 번째 바이트가 0x0A(출발지 주소)인지 확인
            if (check[2] != 0x01)
            {
                Debug.WriteLine("출발지 주소를 다시 확인하세요.", "통신 실패");
                throw new ArgumentException("Invalid Source Address");
            }

        }

        /// <summary>
        /// [ByteStream] 클래스: [ByteStream]에서 [비트]와 [바이트]를 효율적으로 추출하기 위한 유틸리티 클래스
        /// </summary>
        public class ByteStream
        {
            private readonly byte[] _buffer;
            private readonly int _offset;
            private readonly int _length;

            public ByteStream(byte[] buffer, int offset, int length)
            {
                _buffer = buffer;
                _offset = offset;
                _length = length;
            }

            /// <summary>
            /// 1) [GetBits] 메서드: [단일 바이트] 추출
            /// [ByteStream] 특정 위치 => bitCount 추출
            /// </summary>
            /// <param name="byteIndex">바이트 인덱스</param>
            /// <param name="bitPosition">시작 비트 위치</param>
            /// <param name="bitCount">추출할 비트 수</param>
            /// <returns>추출된 여러 비트 값</returns>
            public byte GetBits(int byteIndex, int bitPosition, int bitCount)
            {
                // [byteIndex]가 배열의 끝을 초과하지 않는지 확인
                // [bitPosition]은 [0 ~ 7]번 비트까지 존재
                // [bitPosition]은 음수가 될 수 없음.
                // [bitCount]는 추출할 비트 수이므로,
                // [bitCount]는 1보다 작을 수 없으며,
                // [bitPosition] + [bitCount]의 합은 8보다 작아야 함.
                if (byteIndex >= _length || bitPosition < 0 || bitCount < 1 || (bitPosition + bitCount) > 8)
                {
                    throw new ArgumentOutOfRangeException(nameof(byteIndex), "Parameters are out of range.");
                }
                int mask = (1 << bitCount) - 1;
                return (byte)((_buffer[byteIndex] >> bitPosition) & mask);
            }

            /// <summary>
            /// 2) [Get] 메서드: [단일 바이트] 추출
            /// [ByteStream] 특정 위치 => [1 Byte] 추출 목적!
            /// </summary>
            /// <param name="byteIndex">바이트 인덱스</param>
            /// <returns>해당 바이트 값</returns>
            public byte Get(int byteIndex)
            {
                // [byteIndex]가 배열의 끝을 초과하지 않는지 확인
                return byteIndex >= _length ? throw new ArgumentOutOfRangeException(nameof(byteIndex)) : _buffer[byteIndex];
            }

            /// <summary>
            /// 3) [GetBytes] 메서드: [다수 바이트] 추출
            /// [ByteStream] 특정 위치 => [byteCount] 추출 목적!
            /// </summary>
            /// <param name="startIndex">시작 인덱스</param>
            /// <param name="byteCount">추출할 바이트 수</param>
            /// <returns>추출된 바이트 배열</returns>
            public byte[] GetBytes(int startIndex, int byteCount)
            {
                // [startIndex] + [byteCount]의 합은 배열의 끝을 초과할 수 없음.
                if (startIndex + byteCount > _length)
                {
                    throw new ArgumentOutOfRangeException("Invalid startIndex or byteCount.");
                }
                byte[] result = new byte[byteCount];
                Array.Copy(_buffer, startIndex, result, 0, byteCount);
                return result;
            }

        }

        public class FlightControlField
        {
            /// <summary>
            /// [Mode override]
            /// [Byte #5.] 7번째 비트
            /// </summary>
            public byte ModeOverride { get; set; }

            /// <summary>
            /// [Flight mode]
            /// [Byte #5.] 6~5번째 비트
            /// </summary>
            public byte FlightMode { get; set; }

            /// <summary>
            /// [Mode engage]
            /// [Byte #5.] 4~1번째 비트
            /// </summary>
            public byte ModeEngage { get; set; }

            /// <summary>
            /// [Flap Override]
            /// [Byte #6.] 7번째 비트
            /// </summary>
            public byte FlapOverride { get; set; }

            /// <summary>
            /// [플랩각 조종 명령]
            /// [Byte #6.] 6~1번째 비트
            /// </summary>
            public byte FlapAngle { get; set; }

            /// <summary>
            /// [Wing Tilt Override]
            /// [Byte #7.] 7번째 비트
            /// </summary>
            public byte WingTiltOverride { get; set; }

            /// <summary>
            /// [틸트각 조종 명령]
            /// [Byte #7.] 6~0번째 비트
            /// </summary>
            public byte TiltAngle { get; set; }

            /// <summary>
            /// [노브 속도 조종명령]
            /// [Byte #8.]
            /// </summary>
            public byte KnobSpeed { get; set; }

            /// <summary>
            /// [노브 고도 조종명령]
            /// [Byte #9.]
            /// </summary>
            public byte KnobAltitude { get; set; }

            /// <summary>
            /// [노브 방위 조종명령]
            /// [Byte #10.]
            /// </summary>
            public byte KnobHeading { get; set; }

            /// <summary>
            /// [스틱 고도 조종명령]
            /// [Byte #11.]
            /// </summary>
            public byte StickThrottle { get; set; }

            /// <summary>
            /// [스틱 횡방향 속도 조종명령]
            /// [Byte #12.]
            /// </summary>
            public byte StickRoll { get; set; }

            /// <summary>
            /// [스틱 종방향 속도 조종명령]
            /// [Byte #13.]
            /// </summary>
            public byte StickPitch { get; set; }

            /// <summary>
            /// [스틱 방위 조종명령]
            /// [Byte #14.]
            /// </summary>
            public byte StickYaw { get; set; }

            /// <summary>
            /// [Longitude of Landing point]
            /// [Byte #15. ~ Byte #18.] = [4 Byte]
            /// </summary>
            public byte[] LonOfLP { get; set; }

            /// <summary>
            /// [Latitude of Landing point]
            /// [Byte #19. ~ Byte #22.] = [4 Byte]
            /// </summary>
            public byte[] LatOfLP { get; set; }

            /// <summary>
            /// [Altitude of Landing point]
            /// [Byte #23. ~ Byte #24.] = [2 Byte]
            /// </summary>
            public byte[] AltOfLP { get; set; }

            /// <summary>
            /// [Engine Start / Stop]
            /// [Byte #25.] 7번째 비트
            /// </summary>
            public byte EngineStartStop { get; set; }

            /// <summary>
            /// [구조장비 투하 전 개폐명령]
            /// [Byte #25.] 0번째 비트
            /// </summary>
            public byte RaftDrop { get; set; }
        }

    }

    public static class FlightControlFieldExtention
    {
        public static string ModeOverrideParser(this byte modeOverrideByte)
        {
            int modeOverrideByteToInt = modeOverrideByte;
            // [0x01, 0x00] => [ON, OFF] 변환 공식
            return modeOverrideByteToInt == 1 ? "ON(default)" : "OFF";
        }

        public static string FlightModeParser(this byte flightModeByte)
        {
            int flightModeByteToInt = flightModeByte;
            // [0x00, ~ 0x03] => [Preprogram(Default) ~ Manual(CAS)] 변환 공식
            switch (flightModeByteToInt)
            {
                case 0:
                    return "Preprogram(Default)";
                case 1:
                    return "WPT Navigation";
                case 2:
                    return "Knob";
                case 3:
                    return "Manual(CAS)";
                default:
                    return "Unknown";
            }

        }

        public static string ModeEngageParser(this byte modeEngageByte)
        {
            int modeEngageByteToInt = modeEngageByte;
            // [0x00 ~ 0x08] => [No action(default) ~ Mission reschedule] 변환 공식
            switch (modeEngageByteToInt)
            {
                case 0:
                    return "No action(default)";
                case 1:
                    return "Auto take off(Start preprogrammed mission)";
                case 2:
                    return "Transition_F2M(Fixed to multi)";
                case 3:
                    return "Transition_M2F(Multi to fixed)";
                case 4:
                    return "Jump to waypoint";
                case 5:
                    return "Return to base(=Auto landing)";
                case 6:
                    return "Start mission";
                case 7:
                    return "Hold current position";
                case 8:
                    return "Mission reschedule";
                default:
                    return "Unknown";
            }

        }

        public static string FlapOverrideParser(this byte flapOverrideByte)
        {
            int flapOverrideByteToInt = flapOverrideByte;
            // [0x01, 0x00] => [ON, OFF] 변환 공식
            return flapOverrideByteToInt == 1 ? "ON(default)" : "OFF";
        }

        public static string FlapAngleParser(this byte flapAngleByte)
        {
            int flapAngleByteToInt = flapAngleByte;
            // [0x00, 0x28] => [0, 40] 변환 공식, res = 2
            int flapAngleToInt = (flapAngleByteToInt * 2) - 40;
            return (flapAngleByteToInt <= 40) ? $"{flapAngleToInt}°(도)" : "Unknown";
        }

        public static string WingTiltOverrideParser(this byte wingTiltOverrideByte)
        {
            int wingTiltOverrideByteToInt = wingTiltOverrideByte;
            // [0x01, 0x00] => [ON, OFF] 변환 공식
            return wingTiltOverrideByteToInt == 1 ? "ON" : "OFF";
        }

        public static string TiltAngleParser(this byte tiltAngleByte)
        {
            int tiltAngleByteToInt = tiltAngleByte;
            // [0x00, 0x5A] => [0, 90] 변환 공식, res = 1
            return (tiltAngleByteToInt <= 90) ? $"{tiltAngleByte}°(도)" : "Unknown";
        }

        public static string KnobSpeedParser(this byte knobSpeedByte)
        {
            // 속도값 [1 Byte] To [uint]로 변환
            uint knobSpeedByteToUInt = knobSpeedByte;
            // [0x00, 0xFA] => [0, 250] 변환 공식, res = 1
            uint knobSpeedToUint = knobSpeedByteToUInt * 1;
            return (knobSpeedByteToUInt <= 250) ? $"{knobSpeedToUint} (km/h)" : "Unknown";
        }

        public static string KnobAltitudeParser(this byte knobAltitudeByte)
        {
            // 고도값 [1 Byte] To [uint]로 변환
            uint knobAltitudeByteToUInt = knobAltitudeByte;
            // [0x00, 0xC8] => [0, 200] 변환 공식, res = 15
            uint knobAltitudeToUint = knobAltitudeByteToUInt * 15;
            return (knobAltitudeByteToUInt <= 200) ? $"{knobAltitudeToUint} (m)" : "Unknown";
        }

        public static string KnobHeadingParser(this byte knobHeadingByte)
        {
            // 방위값 [1 Byte] To [uint]로 변환
            uint knobHeadingParserToUInt = knobHeadingByte;
            // [0x00, 0xB4] => [0, 360] 변환 공식, res = 2
            uint knobHeadingToUInt = knobHeadingParserToUInt * 2;
            return (knobHeadingParserToUInt <= 358) ? $"{knobHeadingToUInt}°(도)" : "Unknown";
        }

        public static string StickThrottleParser(this byte stickThrottleByte)
        {
            // 고도값 [1 Byte] To [uint]로 변환
            uint stickThrottleByteToUInt = stickThrottleByte;
            // [0x00, 0xC8] => [0, 1] 변환 공식, res = 0.005
            double stickThrottleToDouble = stickThrottleByteToUInt * 0.005;
            return (stickThrottleByteToUInt <= 200) ? $"{stickThrottleToDouble:F3}" : "Unknown";
        }

        public static string StickRollParser(this byte stickRollByte)
        {
            // 속도값 [1 Byte] To [uint]로 변환!
            uint stickRollByteToUInt = stickRollByte;
            // [0x00, 0xC8] => [-1, 1] 변환 공식, res = 0.01
            double stickRollToDouble = (stickRollByteToUInt * 0.01) - 1;
            return (stickRollByteToUInt <= 200) ? $"{stickRollToDouble:F2}" : "Unknown";
        }

        public static string StickPitchParser(this byte stickPitchByte)
        {
            // 속도값 [1 Byte] To [uint]로 변환!
            uint stickPitchByteToUInt = stickPitchByte;
            // [0x00, 0xC8] => [-1, 1] 변환 공식, res = 0.01
            double stickPitchToDouble = (stickPitchByteToUInt * 0.01) - 1;
            return (stickPitchByteToUInt <= 200) ? $"{stickPitchToDouble:F2}" : "Unknown";
        }

        public static string StickYawParser(this byte stickYawByte)
        {
            // 방위값 [1 Byte] To [uint]로 변환!
            uint stickYawByteToUInt = stickYawByte;
            // [0x00, 0xC8] => [-1, 1] 변환 공식, res = 0.01
            double stickYawToDouble = (stickYawByteToUInt * 0.01) - 1;
            return (stickYawByteToUInt <= 200) ? $"{stickYawToDouble:F2}" : "Unknown";
        }

        public static string LonOfLPParser(this byte[] lonOfLPByte)
        {
            if (lonOfLPByte.Length != 4)
            {
                return "Invalid Bytes!";
            }

            // # 경도값 [4 Byte] To [uint]로 변환 #
            uint lonOfLPToInt = BitConverter.ToUInt32(lonOfLPByte, 0);
            // [리틀 엔디안]으로 들어온 경우에, [바이트 배열]을 리버스 후, [빅 엔디안] 변환
            // 즉, 클라이언트 측에서 데이터를 보낼 때, [빅 엔디안]으로 보내준다는 의미
            // 엔디안 방식만 맞춰서 정확히 변환되면, 최종 UI 값은 엔디안 여부와 상관없이 동일하게 표시
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lonOfLPByte);
            }
            // [0x00000000, 0xD693A400] => [-180, 180] 변환 공식, res = 0.0000001
            double lonOfLPInDegree = (lonOfLPToInt * 0.0000001) - 180.0;
            return (lonOfLPToInt <= 3600000000) ? $"{lonOfLPInDegree:F7}°(도)" : "Unknown";
        }

        public static string LatOfLPParser(this byte[] latOfLPByte)
        {
            if (latOfLPByte.Length != 4)
            {
                return "Invalid Bytes!";
            }

            // # 위도값 [4 Byte] To [uInt]로 변환 #
            uint latOfLPToInt = BitConverter.ToUInt32(latOfLPByte, 0);
            // [리틀 엔디안]으로 들어온 경우에, [바이트 배열]을 리버스 후, [빅 엔디안] 변환
            // 즉, 클라이언트 측에서 데이터를 보낼 때, [빅 엔디안]으로 보내준다는 의미
            // 엔디안 방식만 맞춰서 정확히 변환되면, 최종 UI 값은 엔디안 여부와 상관없이 동일하게 표시
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(latOfLPByte);
            }
            // [0x00000000, 0x6B49D200] => [-90, 90] 변환 공식, res = 0.0000001
            double latOfLPInDegree = (latOfLPToInt * 0.0000001) - 90.0;
            return (latOfLPToInt <= 1800000000) ? $"{latOfLPInDegree:F7}°(도)" : "Unknown";
        }

        public static string AltOfLPParser(this byte[] altOfLPByte)
        {
            if (altOfLPByte.Length != 2)
            {
                return "Invalid Bytes!";
            }

            // # 고도값 [2 Byte] To [ushort]로 변환 #
            ushort altOfLPToShort = BitConverter.ToUInt16(altOfLPByte, 0);
            // [리틀 엔디안]으로 들어온 경우에, [바이트 배열]을 리버스 후, [빅 엔디안] 변환
            // 즉, 클라이언트 측에서 데이터를 보낼 때, [빅 엔디안]으로 보내준다는 의미
            // 엔디안 방식만 맞춰서 정확히 변환되면, 최종 UI 값은 엔디안 여부와 상관없이 동일하게 표시
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(altOfLPByte);
            }
            // [0x0000, 0xEA60] => [-500, 1000] 변환 공식, res = 0.025
            double altOfLPInMeters = (altOfLPToShort * 0.025) - 500.0;
            return (altOfLPToShort <= 60000) ? $"{altOfLPInMeters:F3}°(m)" : "Unknown";
        }

        public static string EngineStartStopParser(this byte engineStartStopByte)
        {
            int engineStartStopByteToInt = engineStartStopByte;
            // [0x01, 0x00] => [ON, OFF] 변환 공식
            return engineStartStopByteToInt == 1 ? "ON" : "OFF";
        }

        public static string RaftDropParser(this byte raftDropByte)
        {
            int raftDropByteToInt = raftDropByte;
            // [0x01, 0x00] => [ON, OFF] 변환 공식
            return raftDropByteToInt == 1 ? "ON" : "OFF";
        }

    }

}
