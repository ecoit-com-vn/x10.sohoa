export const environment = {
  production: false,
  // URL của API Gateway (YARP) - HTTP local tránh lỗi development certificate
  apiGatewayUrl: 'http://localhost:5010',
  // URL trực tiếp WorkflowService nếu bypass gateway
  workflowServiceUrl: '',
  ecoScannerWsUrl: 'ws://127.0.0.1:8282',
  ecoScannerDebug: true,
};
