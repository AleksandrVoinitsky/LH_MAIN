using System;
using System.Collections.Generic;
using System.Text;

namespace LH.Main.Unity.Networking
{
    public readonly struct MatchAdmissionPayload
    {
        public Guid MatchId { get; }
        public Guid PlayerId { get; }
        public string ServerId { get; }
        public string Ticket { get; }

        public MatchAdmissionPayload(Guid matchId, Guid playerId, string serverId, string ticket)
        {
            MatchId = matchId;
            PlayerId = playerId;
            ServerId = serverId ?? string.Empty;
            Ticket = ticket ?? string.Empty;
        }

        public static bool TryParse(string json, out MatchAdmissionPayload payload, out string error)
        {
            payload = default;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "payload_required";
                return false;
            }

            if (!TryReadStringObject(json, out Dictionary<string, string> parsed))
            {
                error = "payload_malformed";
                return false;
            }

            parsed.TryGetValue("matchId", out string matchIdText);
            if (!Guid.TryParse(matchIdText, out Guid matchId))
            {
                error = "match_id_invalid";
                return false;
            }

            parsed.TryGetValue("playerId", out string playerIdText);
            if (!Guid.TryParse(playerIdText, out Guid playerId))
            {
                error = "player_id_invalid";
                return false;
            }

            parsed.TryGetValue("serverId", out string serverId);
            if (string.IsNullOrWhiteSpace(serverId))
            {
                error = "server_id_required";
                return false;
            }

            parsed.TryGetValue("ticket", out string ticket);
            if (string.IsNullOrWhiteSpace(ticket))
            {
                error = "ticket_required";
                return false;
            }

            payload = new MatchAdmissionPayload(matchId, playerId, serverId, ticket);
            error = string.Empty;
            return true;
        }

        public string ToJson()
        {
            return "{\"matchId\":\"" + EscapeJsonString(MatchId.ToString())
                + "\",\"playerId\":\"" + EscapeJsonString(PlayerId.ToString())
                + "\",\"serverId\":\"" + EscapeJsonString(ServerId)
                + "\",\"ticket\":\"" + EscapeJsonString(Ticket) + "\"}";
        }

        private static bool TryReadStringObject(string json, out Dictionary<string, string> values)
        {
            values = new Dictionary<string, string>(StringComparer.Ordinal);
            int index = 0;
            SkipWhitespace(json, ref index);
            if (!ReadChar(json, ref index, '{'))
                return false;

            SkipWhitespace(json, ref index);
            if (ReadChar(json, ref index, '}'))
                return IsAtEnd(json, ref index);

            while (index < json.Length)
            {
                if (!TryReadString(json, ref index, out string key))
                    return false;

                SkipWhitespace(json, ref index);
                if (!ReadChar(json, ref index, ':'))
                    return false;

                SkipWhitespace(json, ref index);
                if (!TryReadString(json, ref index, out string value))
                    return false;

                values[key] = value;
                SkipWhitespace(json, ref index);

                if (ReadChar(json, ref index, '}'))
                    return IsAtEnd(json, ref index);

                if (!ReadChar(json, ref index, ','))
                    return false;

                SkipWhitespace(json, ref index);
            }

            return false;
        }

        private static bool TryReadString(string json, ref int index, out string value)
        {
            value = string.Empty;
            if (!ReadChar(json, ref index, '"'))
                return false;

            var builder = new StringBuilder();
            while (index < json.Length)
            {
                char current = json[index++];
                if (current == '"')
                {
                    value = builder.ToString();
                    return true;
                }

                if (current == '\\')
                {
                    if (index >= json.Length)
                        return false;

                    char escaped = json[index++];
                    switch (escaped)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            builder.Append(escaped);
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        default:
                            return false;
                    }

                    continue;
                }

                builder.Append(current);
            }

            return false;
        }

        private static string EscapeJsonString(string value)
        {
            var builder = new StringBuilder();
            foreach (char current in value ?? string.Empty)
            {
                switch (current)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(current);
                        break;
                }
            }

            return builder.ToString();
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
                index++;
        }

        private static bool ReadChar(string json, ref int index, char expected)
        {
            if (index >= json.Length || json[index] != expected)
                return false;

            index++;
            return true;
        }

        private static bool IsAtEnd(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            return index == json.Length;
        }
    }
}
