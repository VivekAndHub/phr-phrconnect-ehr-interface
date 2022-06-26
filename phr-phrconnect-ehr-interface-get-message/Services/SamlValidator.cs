
using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using System.Linq;

namespace Mdrx.PhrHubHub.PHR.PHRConnect.MessageFunction.Services
{
    
    public class SamlValidator : ISamlValidator
    {
        private string ERROR_IN_SAML = "Error processing SAML";
        private string[] AUDIENCE_HUB;
        private string STANDARD_USER = "STANDARDUSER";
        private string ServiceAccount_USER = "ServiceAccountUser";

        public SamlValidator( IConfiguration configuration)
        {
            this.AUDIENCE_HUB = configuration.ShieldAllowedURIs;
        }

        public SamlValidationResult ValidateSamlToken(string base64Saml, X509Certificate2 x509Certificate, bool isServiceAccountToken, TimeSpan maxClockSkew)
        {
            base64Saml = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64Saml));
            var result = new SamlValidationResult();
            try
            {
                if (string.IsNullOrWhiteSpace(base64Saml))
                {
                    result.Message = ERROR_IN_SAML;
                    result.IsParsingSuccessful = false;
                    return result;
                }
                var doc = new System.Xml.XmlDocument
                {
                    XmlResolver = null
                };
                doc.LoadXml(base64Saml);
                result.IsParsingSuccessful = true;
                ValidateSignature(doc, x509Certificate, ref result);
                ValidateAssertions(doc, ref result, isServiceAccountToken, maxClockSkew);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Exception parsing SAML: {e}");
                result.Message = string.Format("{0} - {1}", ERROR_IN_SAML, e);
                result.IsParsingSuccessful = false;
                return result;
            }
            return result;
        }

        private void ValidateSignature(XmlDocument xmlElement, X509Certificate2 x509Certificate, ref SamlValidationResult result)
        {

            try
            {
                XmlNamespaceManager nSpace = new XmlNamespaceManager(xmlElement.NameTable);
                nSpace.AddNamespace("ds", SignedXml.XmlDsigNamespaceUrl);
                XmlElement signNode = (XmlElement)xmlElement.DocumentElement.SelectSingleNode("ds:Signature", nSpace);
                SamlSignedXml samlSignedXml = new SamlSignedXml((XmlElement)xmlElement.DocumentElement, "AssertionID");
                samlSignedXml.LoadXml((XmlElement)signNode);
               
                result.IsSignatureValid = samlSignedXml.CheckSignature(x509Certificate.PublicKey.Key);
                var checkCertificateChain = true;
                if (!Boolean.TryParse(Environment.GetEnvironmentVariable("IsProdEnv"), out checkCertificateChain))
                    throw new ArgumentException("IsProdEnv is error.");
                if (checkCertificateChain)
                    result.IsSignatureValid = x509Certificate.Verify();
            }
            catch (Exception exp)
            {
                result.Message = string.Format("Error ValidateSignature -{0}", exp.Message);
                result.IsSignatureValid = false;
            }
        }

        private void ValidateAssertions(XmlDocument xDoc, ref SamlValidationResult samlValidationResult, bool isServiceAccountToken, TimeSpan maxClockSkew)
        {
            XmlNamespaceManager nSpace = new XmlNamespaceManager(xDoc.NameTable);
            nSpace.AddNamespace("ns1", "urn:oasis:names:tc:SAML:1.0:assertion");
            XmlNode assertionNode = xDoc.SelectSingleNode("ns1:Assertion/ns1:Conditions/ns1:AudienceRestrictionCondition/ns1:Audience", nSpace);
            XmlNode notBefore = xDoc.SelectSingleNode("ns1:Assertion/ns1:Conditions/@NotBefore", nSpace);
            XmlNode notOnOrAfter = xDoc.SelectSingleNode("ns1:Assertion/ns1:Conditions/@NotOnOrAfter", nSpace);
            XmlNode typeOfUser = xDoc.SelectSingleNode("ns1:Assertion/ns1:AttributeStatement/ns1:Attribute[@AttributeName = 'typeofuser']/ns1:AttributeValue", nSpace);
            XmlNode tenantIDNode = xDoc.SelectSingleNode("ns1:Assertion/ns1:AttributeStatement/ns1:Attribute[@AttributeName = 'tenantId']/ns1:AttributeValue", nSpace); 
            string notBeforeValue = notBefore?.InnerText;
            string notOnOrAfterValue = notOnOrAfter?.InnerText;
            string audience = assertionNode?.InnerText;
            string typeOfUserValue = typeOfUser?.InnerText;
            samlValidationResult.TenantID = tenantIDNode?.InnerText;


            ValidateAudience(audience, samlValidationResult);
            ValidateExpiration(notBeforeValue, notOnOrAfterValue, samlValidationResult, maxClockSkew);
            ValidateTypeOfUser(typeOfUserValue, samlValidationResult, isServiceAccountToken ? ServiceAccount_USER : STANDARD_USER);

        }

        private void ValidateAudience(string audience, SamlValidationResult samlValidationResult)
        {

            if (audience != null && AUDIENCE_HUB.Contains(audience.ToLower()))
            {
                samlValidationResult.IsAudienceValid = true;
            }
            else
            {
                samlValidationResult.IsAudienceValid = false;
                samlValidationResult.Message = "Audience is missing or invalid from SAML";
                return;
            }

        }

        private void ValidateExpiration(string notBefore, string notOnOrAfter, SamlValidationResult samlValidationResult, TimeSpan maxClockSkew)
        {
            
            samlValidationResult.IsDateRangeValid = false;
            
            if (notBefore == null || notOnOrAfter == null)
            {
                samlValidationResult.Message = "Time condition not found in SAML token.";
                return;
            }
            DateTime now = DateTime.UtcNow;
            DateTime dtNotBefore = DateTime.Parse(notBefore).ToUniversalTime();
            DateTime dtnotOnOrAfter = DateTime.Parse(notOnOrAfter).ToUniversalTime();
            if ((now.Add(maxClockSkew) >= dtNotBefore) && (now.Add(maxClockSkew.Negate()) <= dtnotOnOrAfter))
            {
                samlValidationResult.IsDateRangeValid = true;
            }
            else if (now.Add( maxClockSkew) >= dtNotBefore)
            {
                samlValidationResult.Message = "ValidateLifeTime : Invalid time received in SAML response for notBefore.";
                return;
            }
            else if (now.Add( maxClockSkew.Negate()) <= dtnotOnOrAfter)
            {
                samlValidationResult.Message = "ValidateLifeTime : Invalid time received in SAML response for notOnOrAfter.";
                return;
            }
            else
            {
                samlValidationResult.Message = "The saml token have expired.";
                return;
            }
        }

        private void ValidateTypeOfUser(string typeOfUser, SamlValidationResult samlValidationResult, string expectedTypeOfUser)
        {
            if (!string.IsNullOrWhiteSpace(typeOfUser) && typeOfUser.Equals(expectedTypeOfUser, StringComparison.InvariantCultureIgnoreCase))
            {
                samlValidationResult.IsTypeOfUserValid = true;
            }
            else
            {
                samlValidationResult.IsTypeOfUserValid = false;
                samlValidationResult.Message = "The Type Of User is missing or is Invalid";
                return;
            }

        }

    }
    public class SamlSignedXml : SignedXml
    {
        private string _referenceAttributeId = "";
        public SamlSignedXml(XmlElement element, string referenceAttributeId) : base(element)
        {
            _referenceAttributeId = referenceAttributeId;
        }
        public override XmlElement GetIdElement(XmlDocument document, string idValue)
        {
            var basereturn = base.GetIdElement(document, idValue);
            return (XmlElement)document.SelectSingleNode(string.Format("//*[@{0}='{1}']", _referenceAttributeId, idValue));
        }
    }
}