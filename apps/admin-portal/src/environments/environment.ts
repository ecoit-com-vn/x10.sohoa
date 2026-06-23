export const environment = {
  production: false,
  // URL của API Gateway (YARP) - chạy local port 7112
  apiGatewayUrl: 'https://localhost:7112',
  // URL trực tiếp WorkflowService nếu bypass gateway
  workflowServiceUrl: '',
  ecoScannerWsUrl: 'ws://127.0.0.1:8282',
};
