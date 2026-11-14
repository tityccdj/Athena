# Game Design Document: Battle of NutShell

## 1. Game Overview & Core Vision
*   **Core Experience:** ใช้ทักษะการเล่น 2D Side-Scroll Action เพื่อเอาชนะบอส
*   **Genre:** 2D Side-Scroll, Action, Platformer, Boss Battle
*   **Structure:** ใช้ Scene เดียวสู้กับบอส แบ่งพื้นที่เป็น 3 Rooms, มี UI เมนูชั่วคราวตอนเริ่ม
*   **References:**
    *   *Megaman X:* การเคลื่อนไหวแบบ Side-Scroll, การสู้บอส, Dash, Projectile
    *   *Hollow Knight:* การหาจังหวะบอส, Platforming, จังหวะ Hit & Run

## 2. Global Mechanics
*   **Platforming:** Player และ Boss เคลื่อนที่แบบ Side-Scroll ไม่มีตกเหวตาย
*   **Pause System:** กด ESC เพื่อ Pause ได้ แต่หากกดระหว่าง Boss Fight เกิน 3 ครั้ง จะถูกระงับการ Pause (ป้องกันการสแปม)
*   **Rooms & Camera:**
    *   **Room 1:** Tutorial (กล้องเดินตามปกติ)
    *   **Room 2:** Boss Phase 1-2 (กล้อง Lock กรอบในห้อง)
    *   **Room 3:** Boss Phase 3 (กล้อง Lock กรอบในห้อง)
    *   *Transition:* ระหว่างวิ่งเปลี่ยนห้อง กล้องจะกลับมาเดินตามปกติ
*   **Obstacles:** วางอยู่ในห้อง หาก Player สัมผัสโดนจะรับ **1 Damage**

## 3. Player Mechanics
### Movement
*   เดิน ซ้าย-ขวา ตามปกติ
*   กระโดดได้ 1 ครั้ง (Single Jump)
*   **Dash:** มีคูลดาวน์, เป็น I-Frame (อมตะ), **สามารถพุ่งทะลุตัว Boss ได้**
*   **Wall Slide:** เกาะกำแพงได้ ตัวจะค่อยๆ ไถลลงมาอย่างช้าๆ
*   **Input Buffer:** 0.1 - 0.2 วินาที (เพื่อให้เกมเพลย์ลื่นไหล)

### Health & Survivability
*   **Max HP:** 5 หน่วย (โดนโจมตีลดทีละ 1)
*   **Hit Reaction:** เมื่อโดนดาเมจจะเกิด Knockback และติด I-Frame ตัวกะพริบ 1 วินาที
*   **Death & Respawn:** เมื่อ HP = 0 ตายทันที และเริ่มใหม่ที่ Phase ล่าสุดของบอส (เลือดบอสจะรีเซ็ตกลับเต็ม MAX ของ Phase นั้นๆ)
*   **Heal:** กดใช้งานได้ทันที เพิ่ม HP 2 หน่วย (คูลดาวน์ 30 วินาที)

### Combat
*   **Melee Combo:** โจมตีต่อเนื่องได้ 2 จังหวะ (Hit 2 แรงกว่า Hit 1) มีคูลดาวน์ระหว่างแต่ละ Hit
*   **Dash Attack:** ปล่อย Projectile ทำดาเมจ **2 Damage**

## 4. Boss Mechanics (Global)
*   **Total HP:** 300 หน่วย
*   **Architecture:** แบ่งแต่ละ Phase เป็น GameObject แยกกัน และใช้คำสั่ง `SetActive` สลับตัวเมื่อเปลี่ยน Phase
*   **Hitbox:** แยก Body Hitbox (ชนโดนดาเมจ) และ Attack Hitbox ออกจากกัน
*   **Damage:** การโจมตีทุกท่า รวมถึงการเดินชน ทำ **1 Damage** (ยกเว้นท่าพิเศษที่กำหนดไว้)
*   **AI Logic:**
    *   สุ่มท่าโจมตีโดยอิงจาก "ระยะห่าง" (Distance) ระหว่าง Player และ Boss
    *   จะไม่สุ่มท่าเดิมซ้ำกันเกิน 2 ครั้งติดกัน
    *   ทุกท่าโจมตีจะมี Delay (Anticipation) ก่อนเสมอ โดยใช้ Animator แสดงท่า Charge และตัวอาจจะกะพริบเพื่อเตือน (Telegraphing)

## 5. Boss Phases Breakdown
### Phase 1: Orc + Rider
*   **HP Trigger:** 60% - 100%
*   **Movement:** ความเร็วระดับกลาง
*   **Phase Transition:** เมื่อ HP หมดหลอดเฟสนี้ บอสจะติด I-Frame เพื่อเปลี่ยนเป็น Phase 2
*   **Attacks (3 ท่า):**
    1.  *Charge 1 วินาที:* เพิ่มความเร็ววิ่งและโจมตี (Player อยู่ใกล้หรือไกล)
    2.  *Charge 1 วินาที:* โจมตีด้านหน้า (Player อยู่ใกล้)
    3.  *Charge 1 วินาที:* โจมตีรอบตัว (Player อยู่ใกล้)

### Phase 2: Orc
*   **HP Trigger:** 20% - 59%
*   **Movement:** ความเร็วระดับกลาง
*   **Phase Transition:** เมื่อตาย ร่างจะกลายเป็นผีและบินไป Room 3. บอสจะติด I-Frame ตลอดทางจนกว่า Player จะวิ่งตามไปถึง Room 3 และแสดง Animation เปิดตัวเสร็จ (ประตูเปิด Player วิ่งเข้า) *หมายเหตุ: เลือด Player ไม่รีเซ็ต แต่คูลดาวน์ Heal ทำงานนับเวลาตามปกติ*
*   **Attacks (3 ท่า):**
    1.  *Charge:* โจมตีด้านหน้า (Player อยู่ใกล้)
    2.  *Charge 1 วินาที:* เพิ่มความเร็ววิ่ง พุ่งตัวหมุนเป็นลูกข่างโจมตีต่อเนื่องระยะเวลาหนึ่ง (Player อยู่ใกล้หรือไกล)
    3.  *Charge:* โจมตีด้านหน้า และปล่อย Shock wave (Player อยู่ใกล้หรือไกล)

### Phase 3: Necromancer
*   **HP Trigger:** 1% - 19%
*   **Movement:** ขยับไวมาก, **ลอยกลางอากาศ (บินทะลุสิ่งกีดขวางได้)**
*   **Win State:** เมื่อตาย ฉากจบจะทำ Effect Slow-motion + Zoom-in และค่อยๆ Zoom-out กลับมาปกติ
*   **Attacks (2 ท่า):**
    1.  *Melee:* โจมตีด้านหน้า (ระหว่างปล่อยท่าโจมตี ตัวบอสจะเคลื่อนที่ช้าลงเล็กน้อย) (Player อยู่ใกล้)
    2.  *Meteor:* เรียกอุกกาบาตตกลงมาตรงตำแหน่งที่ Player ยืนอยู่ (Player อยู่ไกล)