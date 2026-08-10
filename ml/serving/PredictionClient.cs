using Google.Cloud.AIPlatform.V1;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Polly;
using Polly.CircuitBreaker;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace His.Hope.ML.Serving
{
    public class PredictionClient
    {
        private readonly string _projectId;
        private readonly string _region;
        private readonly string _endpointId;
        private readonly PredictionServiceClient _client;
        private readonly AsyncCircuitBreakerPolicy _circuitBreaker;

        public PredictionClient(string projectId, string region, string endpointId)
        {
            _projectId = projectId;
            _region = region;
            _endpointId = endpointId;
            _client = new PredictionServiceClientBuilder
            {
                Endpoint = $"{region}-aiplatform.googleapis.com"
            }.Build();

            _circuitBreaker = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 3,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (ex, duration) =>
                        Console.WriteLine($"Circuit broken for {duration}"),
                    onReset: () =>
                        Console.WriteLine("Circuit reset"),
                    onHalfOpen: () =>
                        Console.WriteLine("Circuit half-open")
                );
        }

        public async Task<NoShowPrediction> PredictNoShowAsync(NoShowFeatures features)
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                var endpoint = $"projects/{_projectId}/locations/{_region}/endpoints/{_endpointId}";

                var instance = new Value
                {
                    StructValue = new Struct
                    {
                        Fields =
                        {
                            ["age_group"] = new Value { NumberValue = features.AgeGroup },
                            ["gender"] = new Value { NumberValue = features.Gender },
                            ["total_encounters"] = new Value { NumberValue = features.TotalEncounters },
                            ["chronic_conditions_count"] = new Value { NumberValue = features.ChronicConditionsCount },
                            ["lead_time_days"] = new Value { NumberValue = features.LeadTimeDays },
                            ["day_of_week"] = new Value { NumberValue = features.DayOfWeek },
                            ["hour_of_day"] = new Value { NumberValue = features.HourOfDay }
                        }
                    }
                };

                var response = await _client.PredictAsync(endpoint, new List<Value> { instance });

                var predictionValue = response.Predictions[0].StructValue.Fields["prediction"].NumberValue;
                var probabilityValue = response.Predictions[0].StructValue.Fields["probability"].NumberValue;

                return new NoShowPrediction
                {
                    WillNoShow = predictionValue > 0.5,
                    Probability = (float)probabilityValue,
                    Confidence = (float)Math.Max(probabilityValue, 1 - probabilityValue)
                };
            });
        }

        public async Task<ReadmissionPrediction> PredictReadmissionAsync(string patientId)
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                var endpoint = $"projects/{_projectId}/locations/{_region}/endpoints/readmission-endpoint";

                var instance = new Value
                {
                    StructValue = new Struct
                    {
                        Fields =
                        {
                            ["patient_id"] = new Value { StringValue = patientId }
                        }
                    }
                };

                var response = await _client.PredictAsync(endpoint, new List<Value> { instance });

                var risk30d = response.Predictions[0].StructValue.Fields["readmission_risk_30d"].NumberValue;
                var risk90d = response.Predictions[0].StructValue.Fields["readmission_risk_90d"].NumberValue;

                return new ReadmissionPrediction
                {
                    Risk30Day = (float)risk30d,
                    Risk90Day = (float)risk90d,
                    TopFactors = response.Predictions[0].StructValue.Fields["top_factors"].ListValue.Values
                };
            });
        }
    }

    public class NoShowFeatures
    {
        public double AgeGroup { get; set; }
        public double Gender { get; set; }
        public double TotalEncounters { get; set; }
        public double ChronicConditionsCount { get; set; }
        public double LeadTimeDays { get; set; }
        public double DayOfWeek { get; set; }
        public double HourOfDay { get; set; }
    }

    public class NoShowPrediction
    {
        public bool WillNoShow { get; set; }
        public float Probability { get; set; }
        public float Confidence { get; set; }
    }

    public class ReadmissionPrediction
    {
        public float Risk30Day { get; set; }
        public float Risk90Day { get; set; }
        public RepeatedField<Value> TopFactors { get; set; }
    }
}
