using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace VectorCanTest.Logic.Vector
{
    // ── Job / Result models ──────────────────────────────────────────────────

    public sealed class DtcReadJob
    {
        public string Node { get; set; }
        public uint RequestId { get; set; }
        public uint ResponseId { get; set; }
        public string DtcMappingFile { get; set; }
    }

    public sealed class DtcReadResult
    {
        public string Node { get; set; }
        public uint RequestId { get; set; }
        public uint ResponseId { get; set; }
        public string ResultType { get; set; }
        public string DtcCode { get; set; }
        public string DtcName { get; set; }
        public byte? Status { get; set; }
        public byte? Nrc { get; set; }
        public byte[] RawPayload { get; set; } = Array.Empty<byte>();
    }

    // ── DTC name mapping (JSON file cache) ──────────────────────────────────

    public sealed class DtcMappingRepository
    {
        private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _cache
            = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, string> Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("DTC mapping file path is required.", nameof(filePath));

            if (_cache.TryGetValue(filePath, out var cached))
                return cached;

            if (!File.Exists(filePath))
                throw new FileNotFoundException("DTC mapping JSON file not found.", filePath);

            try
            {
                string json = File.ReadAllText(filePath);
                var data    = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (data == null)
                    throw new InvalidOperationException($"Invalid DTC mapping JSON: {filePath}");

                _cache[filePath] = data;
                return data;
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                throw new InvalidOperationException($"Bad JSON in DTC mapping file '{filePath}'.", ex);
            }
        }
    }

    // ── High-level DTC read orchestrator ────────────────────────────────────

    public sealed class DtcReadService
    {
        private readonly UdsClient _udsClient;
        private readonly DtcMappingRepository _mappingRepo;

        public DtcReadService(UdsClient udsClient, DtcMappingRepository mappingRepo)
        {
            _udsClient  = udsClient  ?? throw new ArgumentNullException(nameof(udsClient));
            _mappingRepo = mappingRepo ?? throw new ArgumentNullException(nameof(mappingRepo));
        }

        public IReadOnlyList<DtcReadResult> ReadDtc(IReadOnlyList<DtcReadJob> jobs)
        {
            var results = new List<DtcReadResult>();

            foreach (DtcReadJob job in jobs)
            {
                try
                {
                    byte[] response = _udsClient.ReadDtcByStatusMask(job.RequestId, job.ResponseId, 0x09);
                    IReadOnlyList<DtcRecord> records = DtcDecoder.ParseReadDtcResponse(response);

                    if (records.Count == 0)
                    {
                        results.Add(Base(job, "No DTC", response));
                        continue;
                    }

                    var dtcMap = string.IsNullOrEmpty(job.DtcMappingFile)
                        ? null
                        : _mappingRepo.Load(job.DtcMappingFile);

                    foreach (DtcRecord rec in records)
                    {
                        string name = null;
                        dtcMap?.TryGetValue(rec.Code, out name);

                        results.Add(new DtcReadResult
                        {
                            Node       = job.Node,
                            RequestId  = job.RequestId,
                            ResponseId = job.ResponseId,
                            ResultType = "DTC",
                            DtcCode    = rec.Code,
                            DtcName    = name ?? "Not defined",
                            Status     = rec.Status,
                            RawPayload = response
                        });
                    }
                }
                catch (TimeoutException)
                {
                    results.Add(Base(job, "No response", Array.Empty<byte>()));
                }
                catch (UdsNegativeResponseException ex)
                {
                    results.Add(new DtcReadResult
                    {
                        Node       = job.Node,
                        RequestId  = job.RequestId,
                        ResponseId = job.ResponseId,
                        ResultType = "Negative Response",
                        Nrc        = ex.Nrc
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new DtcReadResult
                    {
                        Node       = job.Node,
                        RequestId  = job.RequestId,
                        ResponseId = job.ResponseId,
                        ResultType = "Error",
                        DtcName    = ex.Message
                    });
                }
            }

            return results;
        }

        private static DtcReadResult Base(DtcReadJob job, string type, byte[] payload) =>
            new DtcReadResult
            {
                Node       = job.Node,
                RequestId  = job.RequestId,
                ResponseId = job.ResponseId,
                ResultType = type,
                RawPayload = payload
            };
    }
}
