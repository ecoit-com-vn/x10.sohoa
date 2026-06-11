using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using EvnHanoi.WorkflowService.Core.Interfaces;

namespace EvnHanoi.WorkflowService.Infrastructure.Services
{
    public class BpmnValidatorService : IBpmnValidatorService
    {
        public List<string> Validate(string? xmlString)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(xmlString))
            {
                errors.Add("Cấu hình XML quy trình trống.");
                return errors;
            }

            try
            {
                XDocument doc = XDocument.Parse(xmlString);

                // Helper to find elements by LocalName, ignoring namespace prefixes
                List<XElement> GetElementsByLocalName(string localName)
                {
                    return doc.Descendants().Where(e => e.Name.LocalName == localName).ToList();
                }

                // 1. Phải có duy nhất 1 Start Event
                var startEvents = GetElementsByLocalName("startEvent");
                if (startEvents.Count == 0)
                {
                    errors.Add("Quy trình phải có điểm bắt đầu (Start Event).");
                }
                else if (startEvents.Count > 1)
                {
                    errors.Add("Quy trình chỉ được phép có duy nhất 1 điểm bắt đầu (Start Event).");
                }

                // 2. Phải có ít nhất 1 End Event
                var endEvents = GetElementsByLocalName("endEvent");
                if (endEvents.Count == 0)
                {
                    errors.Add("Quy trình phải có ít nhất 1 điểm kết thúc (End Event).");
                }

                // Find all sequence flows to map connections
                var sequenceFlows = GetElementsByLocalName("sequenceFlow");
                var incomingMap = new Dictionary<string, int>();
                var outgoingMap = new Dictionary<string, int>();

                foreach (var flow in sequenceFlows)
                {
                    string? sourceRef = flow.Attribute("sourceRef")?.Value;
                    string? targetRef = flow.Attribute("targetRef")?.Value;

                    if (!string.IsNullOrEmpty(sourceRef))
                    {
                        outgoingMap[sourceRef] = outgoingMap.GetValueOrDefault(sourceRef, 0) + 1;
                    }
                    if (!string.IsNullOrEmpty(targetRef))
                    {
                        incomingMap[targetRef] = incomingMap.GetValueOrDefault(targetRef, 0) + 1;
                    }
                }

                // Types of nodes to validate connection (Task/Gateway)
                // Rule 3: Tất cả các Node (Task/Gateway) phải được kết nối: Không được phép có Node nằm "bơ vơ" không có mũi tên đi vào (incoming) hoặc đi ra (outgoing).
                var taskTypes = new HashSet<string>
                {
                    "task", "userTask", "serviceTask", "scriptTask",
                    "sendTask", "receiveTask", "manualTask", "businessRuleTask", "callActivity"
                };
                var gatewayTypes = new HashSet<string>
                {
                    "exclusiveGateway", "parallelGateway", "inclusiveGateway", "eventBasedGateway", "complexGateway"
                };

                var allNodes = doc.Descendants()
                    .Where(e => taskTypes.Contains(e.Name.LocalName) || gatewayTypes.Contains(e.Name.LocalName))
                    .ToList();

                foreach (var node in allNodes)
                {
                    string? id = node.Attribute("id")?.Value;
                    if (!string.IsNullOrEmpty(id))
                    {
                        string name = node.Attribute("name")?.Value ?? id;
                        int incomingCount = incomingMap.GetValueOrDefault(id, 0);
                        int outgoingCount = outgoingMap.GetValueOrDefault(id, 0);

                        if (incomingCount == 0 && outgoingCount == 0)
                        {
                            errors.Add($"Node '{name}' hoàn toàn không được kết nối (không có mũi tên đi vào và đi ra).");
                        }
                        else if (incomingCount == 0)
                        {
                            errors.Add($"Node '{name}' không có mũi tên đi vào (incoming).");
                        }
                        else if (outgoingCount == 0)
                        {
                            errors.Add($"Node '{name}' không có mũi tên đi ra (outgoing).");
                        }
                    }
                }

                // Rule 4: Exclusive Gateway phải có đúng đường ra (tối đa 2 outgoing)
                var exclusiveGateways = GetElementsByLocalName("exclusiveGateway");
                foreach (var gw in exclusiveGateways)
                {
                    string? id = gw.Attribute("id")?.Value;
                    if (!string.IsNullOrEmpty(id))
                    {
                        string name = gw.Attribute("name")?.Value ?? id;
                        int outgoingCount = outgoingMap.GetValueOrDefault(id, 0);
                        if (outgoingCount == 0)
                        {
                            errors.Add($"Exclusive Gateway '{name}' phải có đường ra.");
                        }
                        else if (outgoingCount > 2)
                        {
                            errors.Add($"Exclusive Gateway '{name}' chỉ được phép có tối đa 2 đường ra.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Định dạng XML không hợp lệ: {ex.Message}");
            }

            return errors;
        }
    }
}
