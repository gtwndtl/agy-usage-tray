# Antigravity Usage Tray (Windows)

โปรแกรมตรวจสอบ Quota และ Usage ของ Google Antigravity (`agy`) บน Windows โดยย่อการทำงานไว้ที่ System Tray (Taskbar Notification Area)

## ฟีเจอร์หลัก
1. **System Tray Icon (Taskbar)**:
   - ไอคอนสถานะที่มุมขวาล่างของหน้าจอ
   - เปลี่ยนสีตามระดับโควตาต่ำสุด (ฟ้า/เขียว = ปกติ, ส้ม = ต่ำกว่า 50%, แดง = ต่ำกว่า 20%)
2. **Hover Tooltip**:
   - เมื่อเอาเมาส์ชี้ที่ไอคอน จะแสดงสรุปเปอร์เซ็นต์โควตาคงเหลือทันที:
     ```text
     AGY Quota:
     Gemini: 91%(5h) / 97%(Wk)
     Claude: 100%(5h) / 53%(Wk)
     Argon: 100%
     ```
3. **Pop-up Dashboard (คลิกซ้าย)**:
   - หน้าต่าง Flyout ทรงโมเดิร์น (สไตล์ Windows 11 Dark Mode) ลอยขึ้นเหนือ Taskbar
   - แสดงการ์ดแยกตามกลุ่มโมเดล (Gemini Models, Claude & GPT Models, Gemini 4 Argon)
   - มี Progress Bar สีตามระดับโควตา, เปอร์เซ็นต์, และเวลานับถอยหลังก่อนรีเซ็ต (เช่น `Resets in 3h 22m`)
   - ปุ่ม **↻ Refresh** เพื่อดึงข้อมูลล่าสุดทันที
   - ตัวเลือกตั้งเวลา Auto-refresh (5 นาที, 10 นาที, 15 นาที, 30 นาที, 1 ชั่วโมง, หรือ Manual)
   - ตัวเลือก **Start on boot** เพื่อให้เปิดอัตโนมัติพร้อม Windows
4. **Context Menu (คลิกขวา)**:
   - Open Dashboard
   - Refresh Now
   - Auto-Refresh Interval
   - Start with Windows
   - Exit

## วิธีเปิดใช้งาน
- ดับเบิ้ลคลิกไฟล์ `run.bat` หรือเปิดไฟล์ `publish\AgyUsageTray.exe`
