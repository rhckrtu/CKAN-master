using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using log4net;
using Newtonsoft.Json.Linq;
using CKAN.NetKAN.Model;

namespace CKAN.NetKAN.Transformers
{
    /// <summary>
    /// An <see cref="ITransformer"/> that allows simple editing of the version number using regular expressions.
    /// </summary>
    internal sealed class VersionEditTransformer : ITransformer
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(VersionEditTransformer));

        public string Name => "version_edit";

        public IEnumerable<Metadata> Transform(Metadata metadata, TransformOptions opts)
        {
            var json = metadata.Json();

            var versionEditInfo = GetVersionEditInfo(json);
            if (versionEditInfo != null)
            {
                Log.InfoFormat("Executing version edit transformation");
                Log.DebugFormat("Input metadata:{0}{1}", Environment.NewLine, json);

                var findRegex = new Regex(versionEditInfo.Find);
                if (findRegex.IsMatch(versionEditInfo.Version))
                {
                    string version = new Regex(versionEditInfo.Find)
                        .Replace(versionEditInfo.Version, versionEditInfo.Replace);

                    var versionPieces = json["x_netkan_version_pieces"]?.Value<JObject>()
                        .ToObject<Dictionary<string, string>>();

                    if (versionPieces != null)
                    {
                        version = versionPieces.Aggregate(
                            version,
                            (v, kvp) =>
                            {
                                Log.Debug($"Replacing ${{{kvp.Key}}} with {kvp.Value} in {v}");
                                return v.Replace($"${{{kvp.Key}}}", kvp.Value);
                            });
                    }


                    json["version"] = version;
                }
                else if (versionEditInfo.Strict)
                {
                    throw new Kraken(string.Format(
                        "Could not match version {0} with find pattern {1}",
                        versionEditInfo.Version,
                        versionEditInfo.Find
                    ));
                }

                Log.DebugFormat("Transformed metadata:{0}{1}", Environment.NewLine, json);
            }

            yield return new Metadata(json);
        }

        private static VersionEditInfo GetVersionEditInfo(JObject json)
        {
            if (json.TryGetValue("x_netkan_version_edit", out JToken editProp) && editProp != null)
            {
                if (json.TryGetValue("version", out JToken versionProp))
                {
                    if (versionProp.Type == JTokenType.String)
                    {
                        string find;
                        var replace = "${version}";
                        var strict = true;

                        switch (editProp.Type)
                        {
                            case JTokenType.String:
                                find = (string)editProp;
                                break;
                            case JTokenType.Object:
                                var editObj = (JObject)editProp;

                                JToken findProp;
                                if (editObj.TryGetValue("find", out findProp))
                                {
                                    if (findProp.Type == JTokenType.String)
                                    {
                                        find = (string)findProp;
                                    }
                                    else
                                    {
                                        throw new Kraken("`x_netkan_version_edit` `find` property must be a string");
                                    }
                                }
                                else
                                {
                                    throw new Kraken("`x_netkan_version_edit` must contain `find` property");
                                }

                                JToken replaceProp;
                                if (editObj.TryGetValue("replace", out replaceProp))
                                {
                                    if (replaceProp.Type == JTokenType.String)
                                    {
                                        replace = (string)replaceProp;
                                    }
                                    else
                                    {
                                        throw new Kraken(
                                            "`x_netkan_version_edit` `replace` property must be a string"
                                        );
                                    }
                                }

                                JToken strictProp;
                                if (editObj.TryGetValue("strict", out strictProp))
                                {
                                    if (strictProp.Type == JTokenType.Boolean)
                                    {
                                        strict = (bool)strictProp;
                                    }
                                    else
                                    {
                                        throw new Kraken(
                                            "`x_netkan_version_edit` `strict` property must be a bool"
                                        );
                                    }
                                }

                                break;
                            default:
                                throw new Kraken(
                                    string.Format("Unrecognized `x_netkan_version_edit` value: {0}", editProp)
                                );
                        }

                        return new VersionEditInfo((string)versionProp, find, replace, strict);
                    }
                    else
                    {
                        throw new Kraken("`version` property must be a string");
                    }
                }
                else
                {
                    throw new Kraken("`version` property does not exist to edit");
                }
            }

            return null;
        }

        private sealed class VersionEditInfo
        {
            public string Version { get; private set; }
            public string Find { get; private set; }
            public string Replace { get; private set; }
            public bool Strict { get; private set; }

            public VersionEditInfo(string version, string find, string replace, bool strict)
            {
                Version = version;
                Find = find;
                Replace = replace;
                Strict = strict;
            }
        }
    }
}
