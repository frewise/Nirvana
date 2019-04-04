﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ErrorHandling.Exceptions;
using OptimizedCore;
using VariantAnnotation.IO;
using VariantAnnotation.SA;

namespace SAUtils.Schema
{
    public sealed class SaJsonSchema
    {
        private const string SchemaVersion = "http://json-schema.org/draft-06/schema#";

        public int TotalItems { get; set; }

        private readonly StringBuilder _sb;
        private readonly JsonObject _jsonObject;
        private readonly Dictionary<string, SaJsonKeyAnnotation> _keyAnnotation = new Dictionary<string, SaJsonKeyAnnotation>();
        private IEnumerable<string> Keys { get; set; } 
        // Keys not used to generate the NSA file, but in the Nirvana JSON output
        private string[] NonSaKeys { get; set; } = {};
        internal readonly Dictionary<string, int> KeyCounts = new Dictionary<string, int>();
        private Action<JsonObject, List<string[]>> _jsonStringGenerationAction;
        private bool _finalized;

        internal SaJsonSchema(StringBuilder sb)
        {
            _sb = sb;
            _jsonObject = new JsonObject(sb);
        }

        public static SaJsonSchema Create(StringBuilder sb, string jsonTag, SaJsonValueType primaryType, IEnumerable<string> jsonKeys)
        {
            var jsonSchema = new SaJsonSchema(sb) {Keys = jsonKeys};

            // The root level schema for a SA
            if (jsonTag != null)
            {
                jsonSchema._jsonObject.StartObject();
                jsonSchema.AddSchemaVersion();
                // SA json is an object
                jsonSchema.AddJsonDataType(JsonDataType.Object, null);
                jsonSchema._jsonObject.StartObjectWithKey(jsonTag);
            }

            jsonSchema.AddJsonValueType(primaryType, null);
            return jsonSchema;
        }

        public void SetNonSaKeys(string[] nonSaKeys)
        {
            NonSaKeys = nonSaKeys;
        }

        private void AddJsonValueType(SaJsonValueType jsonValueType, SaJsonSchema objectSchema)
        {
            foreach (var dataType in jsonValueType.JsonDataTypes)
            {
                AddJsonDataType(dataType, objectSchema);
            }
        }

        private void AddJsonDataType(JsonDataType jsonType, SaJsonSchema objectSchema)
        {
            _jsonObject.AddStringValue("type", jsonType.ToTypeString());
            if (jsonType.IsComplexType()) _jsonObject.StartObjectWithKey(jsonType.GetSchemaKey());

            if (jsonType == JsonDataType.Object) _sb.Append(objectSchema);
        }

        private void AddSchemaVersion() => _jsonObject.AddStringValue("$schema", SchemaVersion);

        private SaJsonValueType GetJsonType(string key) => _keyAnnotation[key].ValueType;
        private CustomAnnotationCategories GetCategory(string key) => _keyAnnotation[key].Category;

        public void AddAnnotation(string key, SaJsonKeyAnnotation annotation)
        {
            _keyAnnotation.Add(key, annotation);
            KeyCounts.Add(key, 0);
        }

        public override string ToString()
        {
            if (!_finalized) FinalizeSchema();
            return _sb.ToString();
        }

        private void FinalizeSchema()
        {
            var requiredKeys = new List<string>();

            foreach (string key in Keys)
            {
                int counts = KeyCounts[key];
                if (counts == 0 && !NonSaKeys.Contains(key)) continue;
                // boolean is always considered as optional
                if (counts == TotalItems && !GetJsonType(key).Equals(SaJsonValueType.Bool)) requiredKeys.Add(key);

                OutputKeyAnnotation(key);
            }

            _jsonObject.EndObject();
            OutputRequiredKeys(requiredKeys);
            DisallowExtraProperites();

            _jsonObject.EndAllObjects();
            _finalized = true;
        }

        private void OutputRequiredKeys(IReadOnlyCollection<string> requiredKeys)
        {
            if (requiredKeys.Count > 0) _jsonObject.AddStringValues("required", requiredKeys);
        }

        private void DisallowExtraProperites()
        {
            _jsonObject.AddStringValue("additionalProperties", "false", false);
        }

        private Action<JsonObject, List<string[]>> GetJsonStringGenerationAction()
        {
            var actions = new List<Action<JsonObject, string[]>>();

            foreach (string key in Keys)
            {
                if (NonSaKeys.Contains(key)) continue;
                var intendedType = GetJsonType(key);

                if (intendedType.Equals(SaJsonValueType.String))
                {
                        actions.Add((jsonObject, value) => CountKeyIfAdded(jsonObject.AddStringValue(key, value[0]), key));
                }

                else if (intendedType.Equals(SaJsonValueType.Bool))
                {
                    actions.Add((jsonObject, value) => CountKeyIfAdded(jsonObject.AddBoolValue(key, CheckAndGetBoolFromString(value[0])), key));
                }

                else if (intendedType.Equals(SaJsonValueType.Number))
                {
                    actions.Add((jsonObject, value) =>
                    {
                        var doubleValue = CheckAndGetNullableDoubleFromString(value[0]);
                        CustomAnnotationCategories keyCategory = GetCategory(key);
                        CountKeyIfAdded(keyCategory == CustomAnnotationCategories.AlleleFrequency
                            ? jsonObject.AddDoubleValue(key, doubleValue, "0.######")
                            : jsonObject.AddStringValue(key, value[0], false), key);
                    });
                }

                else if (intendedType.Equals(SaJsonValueType.StringArray))
                {
                    actions.Add((jsonObject, value) => CountKeyIfAdded(jsonObject.AddStringValues(key, value), key));
                }

                else
                {
                    throw new Exception($"Unknown data type {intendedType}");
                }
            }

            return (jsonObject, strings) => {
                foreach (var (action, str) in actions.Zip(strings, (a, b) => (a, b)))
                {
                    action(jsonObject, str);
                }

                TotalItems++;
            };
        }


        public void CountKeyIfAdded(bool keyAdded, string key)
        {
            if (keyAdded) KeyCounts[key]++;
        }

        public string GetJsonString(List<string[]> values)
        {
            if (_jsonStringGenerationAction == null) _jsonStringGenerationAction = GetJsonStringGenerationAction();

            if (values.Count != Keys.Count(x => !NonSaKeys.Contains(x)))
                throw new UserErrorException("Please provide one and only one value for each JSON key.");

            var sb = StringBuilderCache.Acquire();
            var jsonObject = new JsonObject(sb);

            _jsonStringGenerationAction(jsonObject, values);
            
            return StringBuilderCache.GetStringAndRelease(sb);
        }

        internal void OutputKeyAnnotation(string key)
        { 
            _jsonObject.StartObjectWithKey(key);

            var annotation = _keyAnnotation[key];
            AddJsonValueType(annotation.ValueType, annotation.SubSchema);
            int numComplexTypes = annotation.ValueType.JsonDataTypes.Count(x => x.IsComplexType());
            while (numComplexTypes > 0)
            {
                _jsonObject.EndObject();
                numComplexTypes--;
            }

            if (annotation.Category != CustomAnnotationCategories.Unknown)
                _jsonObject.AddStringValue("category", annotation.Category.ToString());
            if (annotation.Description != null)
                _jsonObject.AddStringValue("description", annotation.Description);

            _jsonObject.EndObject();
        }

        internal static bool CheckAndGetBoolFromString(string value)
        {
            switch (value.ToLower())
            {
                case "true":
                    return true;
                case "false":
                case "":
                case ".":
                    return false;
                default:
                    throw new UserErrorException($"{value} is not a valid boolean.");               
            }
        }

        internal static double? CheckAndGetNullableDoubleFromString(string value)
        {
            if (value == "." || value == "") return null;

            if (double.TryParse(value, out double doubleValue))
                return doubleValue;

            throw new UserErrorException($"{value} is not a valid number.");
        }

        public SaJsonSchema GetSubSchema(string key)
        {
            if (!_keyAnnotation.TryGetValue(key, out var annotation))
                throw new KeyNotFoundException($"{key} is not JSON key.");

            return annotation.SubSchema;
        }
    }
}