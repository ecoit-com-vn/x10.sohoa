import pika
import json

connection = pika.BlockingConnection(pika.ConnectionParameters('localhost', 5672, '/orc', pika.PlainCredentials('orcadmin', 'orcpassword123')))
channel = connection.channel()

message = {
    "FileId": 2,
    "FilePath": "0002.pdf",
    "BucketName": "orc",
    "Forms": [
        {
            "FormId": "form-1",
            "Fields": [
                {"FieldName": "ten_tram", "Description": "Tên của trạm biến áp"},
                {"FieldName": "cong_suat_mba", "Description": "Công suất của Máy biến áp"},
                {"FieldName": "dien_ap_mba", "Description": "Cấp điện áp của Máy biến áp"},
                {"FieldName": "to_dau_day", "Description": "Tổ đấu dây của Máy biến áp"},
                {"FieldName": "nha_thau", "Description": "Tên công ty / nhà thầu điện lực"}
            ]
        }
    ]
}

channel.basic_publish(
    exchange='digitization.topic',
    routing_key='ocr.process.task',
    body=json.dumps(message)
)
print("Sent")
connection.close()
