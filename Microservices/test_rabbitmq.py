import pika
import json

connection = pika.BlockingConnection(pika.ConnectionParameters('localhost', 5672, '/orc', pika.PlainCredentials('orcadmin', 'orcpassword123')))
channel = connection.channel()

messages = [
    {
        "FileId": "70eb14b5-65fc-42de-8b2b-586717eb4132",
        "FilePath": "0001.pdf",
        "BucketName": "orc",
        "ProcessOption": "OcrAndExtract",
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
    },
    {
        "FileId": "1b9a9d28-765f-4a0f-90e6-a05e2ed37eb1",
        "FilePath": "0002.pdf",
        "BucketName": "orc",
        "ProcessOption": "ExtractOnly",
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
]

for msg in messages:
    channel.basic_publish(
        exchange='digitization.topic',
        routing_key='ocr.process.task',
        body=json.dumps(msg)
    )
    print(f"Sent {msg['FilePath']}")
print("Sent")
connection.close()
