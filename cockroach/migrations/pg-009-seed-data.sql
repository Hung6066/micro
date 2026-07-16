-- ============================================================================
-- His.Hope EMR - Seed Data for UI Testing
-- Version: 009
-- Description: Realistic Vietnamese hospital seed data
-- Idempotent: uses ON CONFLICT DO NOTHING for all inserts.
-- ============================================================================

-- ============================================================================
-- SECTION 1: IDENTITY SERVICE (identitydb)
-- ============================================================================
-- Password hash for "Password123!" (ASP.NET Core Identity v3+ PBKDF2)
-- Re-generate with: var hasher = new PasswordHasher<IdentityUser>(); hasher.HashPassword(null, "Password123!");

INSERT INTO "AspNetUsers" (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, PhoneNumber, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, FullName, Role, FacilityId, IsActive, CreatedAt) VALUES
-- Admin
('00000000-0000-0000-0000-000000000101', 'admin@hishop.vn', 'ADMIN@HISHOP.VN', 'admin@hishop.vn', 'ADMIN@HISHOP.VN', true, 'AQAAAAIAAYagAAAAEIvMJiFkRNflIyTrS1SxY7x6mHqQo7/JQ2MJxOZ8zKxCv/9QbM3UJmKPfMq0L0KJqg==', '0901000101', false, true, 0, 'Quản Trị Viên', 'Admin', '11111111-1111-1111-1111-111111111111', true, '2026-06-15 08:00:00+00'),
-- Bác sĩ Nguyễn Văn Minh
('00000000-0000-0000-0000-000000000102', 'bacsy.nguyen@hishop.vn', 'BACSY.NGUYEN@HISHOP.VN', 'bacsy.nguyen@hishop.vn', 'BACSY.NGUYEN@HISHOP.VN', true, 'AQAAAAIAAYagAAAAEIvMJiFkRNflIyTrS1SxY7x6mHqQo7/JQ2MJxOZ8zKxCv/9QbM3UJmKPfMq0L0KJqg==', '0901000102', false, true, 0, 'Nguyễn Văn Minh', 'Provider', '11111111-1111-1111-1111-111111111111', true, '2026-06-15 08:00:00+00'),
-- Bác sĩ Trần Thị Lan
('00000000-0000-0000-0000-000000000103', 'bacsy.tran@hishop.vn', 'BACSY.TRAN@HISHOP.VN', 'bacsy.tran@hishop.vn', 'BACSY.TRAN@HISHOP.VN', true, 'AQAAAAIAAYagAAAAEIvMJiFkRNflIyTrS1SxY7x6mHqQo7/JQ2MJxOZ8zKxCv/9QbM3UJmKPfMq0L0KJqg==', '0901000103', false, true, 0, 'Trần Thị Lan', 'Provider', '11111111-1111-1111-1111-111111111111', true, '2026-06-15 08:00:00+00'),
-- Điều dưỡng Lê Thị Hồng
('00000000-0000-0000-0000-000000000104', 'dieuduong.le@hishop.vn', 'DIEUDUONG.LE@HISHOP.VN', 'dieuduong.le@hishop.vn', 'DIEUDUONG.LE@HISHOP.VN', true, 'AQAAAAIAAYagAAAAEIvMJiFkRNflIyTrS1SxY7x6mHqQo7/JQ2MJxOZ8zKxCv/9QbM3UJmKPfMq0L0KJqg==', '0901000104', false, true, 0, 'Lê Thị Hồng', 'Nurse', '11111111-1111-1111-1111-111111111111', true, '2026-06-15 08:00:00+00')
ON CONFLICT (Id) DO NOTHING;

-- ============================================================================
-- SECTION 2: PATIENT SERVICE (patientdb)
-- ============================================================================

-- 2a. "Patients" (8 "Patients" with Vietnamese names)
INSERT INTO "Patients" (PatientId, FirstName, LastName, MiddleName, DateOfBirth, Gender, Phone, Email, Street, District, City, Province, PostalCode, Country, BloodType, Race, MaritalStatus, InsuranceId, NationalId, Occupation, EmergencyContactName, EmergencyContactPhone, IsActive, CreatedAt, UpdatedAt) VALUES
-- P001: Nguyễn Văn A, 45t, male
('00000000-0000-0000-0000-000000000001', 'A', 'Nguyễn', 'Văn', '1981-03-15', 'Male', '0901234567', 'nguyenvana@email.com', '123 Đường Lê Lợi', 'Quận 1', 'Hồ Chí Minh', 'TP. Hồ Chí Minh', '700000', 'Việt Nam', 'A+', 'Kinh', 'Married', 'BHYT-12345-01', '079181001234', 'Kỹ sư xây dựng', 'Nguyễn Thị Hà', '0909234567', true, '2026-07-01 08:00:00+00', NULL),
-- P002: Trần Thị B, 32t, female
('00000000-0000-0000-0000-000000000002', 'B', 'Trần', 'Thị', '1994-07-22', 'Female', '0912345678', 'tranthib@email.com', '456 Đường Nguyễn Huệ', 'Quận Bình Thạnh', 'Hồ Chí Minh', 'TP. Hồ Chí Minh', '700001', 'Việt Nam', 'B+', 'Kinh', 'Single', 'BHYT-12345-02', '079182001234', 'Giáo viên', 'Trần Văn Hùng', '0919345678', true, '2026-07-01 08:00:00+00', NULL),
-- P003: Lê Văn C, 67t, male
('00000000-0000-0000-0000-000000000003', 'C', 'Lê', 'Văn', '1959-01-10', 'Male', '0923456789', 'levanc@email.com', '789 Đường Võ Văn Tần', 'Quận 3', 'Hồ Chí Minh', 'TP. Hồ Chí Minh', '700002', 'Việt Nam', 'O+', 'Kinh', 'Married', 'BHYT-12345-03', '079183001234', 'Hưu trí', 'Lê Văn Tuấn', '0929456789', true, '2026-07-01 08:00:00+00', NULL),
-- P004: Phạm Thị D, 28t, female
('00000000-0000-0000-0000-000000000004', 'D', 'Phạm', 'Thị', '1998-11-05', 'Female', '0934567890', 'phamthid@email.com', '321 Đường Trần Hưng Đạo', 'Quận 5', 'Hồ Chí Minh', 'TP. Hồ Chí Minh', '700003', 'Việt Nam', 'A-', 'Kinh', 'Single', 'BHYT-12345-04', '079184001234', 'Nhân viên văn phòng', 'Phạm Văn Hải', '0939567890', true, '2026-07-01 08:00:00+00', NULL),
-- P005: Hoàng Văn E, 5t, male (pediatric)
('00000000-0000-0000-0000-000000000005', 'E', 'Hoàng', 'Văn', '2021-04-18', 'Male', '0945678901', NULL, '654 Đường Nguyễn Đình Chiểu', 'Quận 2', 'Hồ Chí Minh', 'TP. Hồ Chí Minh', '700004', 'Việt Nam', NULL, 'Kinh', NULL, NULL, NULL, NULL, 'Hoàng Văn Hùng (Bố)', '0949678901', true, '2026-07-01 08:00:00+00', NULL),
-- P006: Vũ Thị F, 52t, female
('00000000-0000-0000-0000-000000000006', 'F', 'Vũ', 'Thị', '1974-08-30', 'Female', '0956789012', 'vuthif@email.com', '987 Đường Hai Bà Trưng', 'Quận Tân Bình', 'Hồ Chí Minh', 'TP. Hồ Chí Minh', '700005', 'Việt Nam', 'AB+', 'Kinh', 'Married', 'BHYT-12345-06', '079186001234', 'Kế toán', 'Vũ Văn Bình', '0959789012', true, '2026-07-01 08:00:00+00', NULL),
-- P007: Đặng Văn G, 38t, male
('00000000-0000-0000-0000-000000000007', 'G', 'Đặng', 'Văn', '1988-02-14', 'Male', '0967890123', 'dangvang@email.com', '147 Đường Phạm Ngọc Thạch', 'Quận 10', 'Hồ Chí Minh', 'TP. Hồ Chí Minh', '700006', 'Việt Nam', 'O-', 'Kinh', 'Married', 'BHYT-12345-07', '079187001234', 'Tài xế', 'Đặng Thị Mai', '0969890123', true, '2026-07-01 08:00:00+00', NULL),
-- P008: Bùi Thị H, 72t, female
('00000000-0000-0000-0000-000000000008', 'H', 'Bùi', 'Thị', '1954-06-25', 'Female', '0978901234', 'buithih@email.com', '258 Đường Trường Chinh', 'Quận 12', 'Hồ Chí Minh', 'TP. Hồ Chí Minh', '700007', 'Việt Nam', 'B-', 'Kinh', 'Widowed', 'BHYT-12345-08', '079188001234', 'Hưu trí', 'Bùi Văn An', '0979901234', true, '2026-07-01 08:00:00+00', NULL)
ON CONFLICT (PatientId) DO NOTHING;

-- 2b. "Allergies"
INSERT INTO "Allergies" (PatientId, AllergyId, Allergen, Reaction, Severity, RecordedDate, IsActive) VALUES
-- Nguyễn Văn A: Penicillin
('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000011', 'Penicillin', 'Phát ban, ngứa toàn thân', 'Moderate', '2026-06-10 09:00:00+00', true),
-- Trần Thị B: Phấn hoa
('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000012', 'Phấn hoa', 'Hắt hơi, chảy nước mũi, ngứa mắt', 'Mild', '2026-06-15 10:00:00+00', true),
-- Trần Thị B: Tôm
('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000013', 'Tôm biển', 'Nổi mề đay, khó thở nhẹ', 'Moderate', '2026-06-15 10:30:00+00', true),
-- Phạm Thị D: Latex
('00000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000014', 'Latex (Cao su)', 'Viêm da tiếp xúc, mẩn đỏ', 'Mild', '2026-06-20 14:00:00+00', true),
-- Vũ Thị F: Sulfa
('00000000-0000-0000-0000-000000000006', '00000000-0000-0000-0000-000000000015', 'Thuốc Sulfa', 'Phát ban, sốt nhẹ', 'Moderate', '2026-05-05 11:00:00+00', true),
-- Vũ Thị F: Ibuprofen
('00000000-0000-0000-0000-000000000006', '00000000-0000-0000-0000-000000000016', 'Ibuprofen', 'Đau dạ dày, buồn nôn', 'Mild', '2026-06-01 09:00:00+00', true),
-- Bùi Thị H: Codeine
('00000000-0000-0000-0000-000000000008', '00000000-0000-0000-0000-000000000017', 'Codeine', 'Buồn nôn, chóng mặt nghiêm trọng', 'Severe', '2026-04-20 08:00:00+00', true),
-- Bùi Thị H: Aspirin
('00000000-0000-0000-0000-000000000008', '00000000-0000-0000-0000-000000000018', 'Aspirin', 'Hen suyễn, khò khè', 'Severe', '2025-11-10 10:00:00+00', true)
ON CONFLICT (PatientId, AllergyId) DO NOTHING;

-- 2c. Medical Conditions
INSERT INTO "MedicalConditions" (PatientId, ConditionId, ConditionName, Icd10Code, OnsetDate, ResolvedDate, IsChronic, Notes, RecordedDate, IsActive) VALUES
-- P001: Nguyễn Văn A
('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000021', 'Tăng huyết áp nguyên phát', 'I10', '2020-03-01', NULL, true, 'Chẩn đoán lần đầu năm 2020, đang điều trị Amlodipine', '2026-07-01 09:00:00+00', true),
('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000022', 'Đái tháo đường type 2', 'E11', '2022-08-15', NULL, true, 'Kiểm soát bằng Metformin 850mg', '2026-07-01 09:00:00+00', true),
-- P003: Lê Văn C
('00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000023', 'Tăng huyết áp', 'I10', '2019-05-01', NULL, true, 'Đang dùng Losartan 50mg', '2026-07-02 10:00:00+00', true),
('00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000024', 'Bệnh phổi tắc nghẽn mạn tính (COPD)', 'J44', '2021-01-10', NULL, true, 'Tiền sử hút thuốc 40 năm, đã bỏ từ 2021', '2026-07-02 10:00:00+00', true),
-- P004: Phạm Thị D
('00000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000025', 'Hen phế quản', 'J45', '2010-06-20', NULL, true, 'Hen dị ứng, kiểm soát bằng Salbutamol khi cần', '2026-07-03 11:00:00+00', true),
-- P006: Vũ Thị F
('00000000-0000-0000-0000-000000000006', '00000000-0000-0000-0000-000000000026', 'Đái tháo đường type 2', 'E11', '2018-11-01', NULL, true, 'Đang dùng Metformin 850mg x 2 lần/ngày', '2026-07-04 08:00:00+00', true),
-- P007: Đặng Văn G
('00000000-0000-0000-0000-000000000007', '00000000-0000-0000-0000-000000000027', 'Mỡ máu cao (Tăng cholesterol máu)', 'E78', '2025-06-01', NULL, true, 'Rối loạn lipid máu hỗn hợp', '2026-07-05 09:00:00+00', true),
-- P008: Bùi Thị H
('00000000-0000-0000-0000-000000000008', '00000000-0000-0000-0000-000000000028', 'Tăng huyết áp', 'I10', '2015-09-01', NULL, true, 'Điều trị lâu dài, nhiều đợt điều chỉnh thuốc', '2026-07-06 10:00:00+00', true),
('00000000-0000-0000-0000-000000000008', '00000000-0000-0000-0000-000000000029', 'Đái tháo đường type 2', 'E11', '2018-04-15', NULL, true, 'Kết hợp Metformin và chế độ ăn', '2026-07-06 10:00:00+00', true),
('00000000-0000-0000-0000-000000000008', '00000000-0000-0000-0000-000000000030', 'Thoái hóa khớp gối', 'M17', '2022-03-20', NULL, true, 'Đau khớp gối hai bên, đi lại khó khăn', '2026-07-06 10:00:00+00', true)
ON CONFLICT (PatientId, ConditionId) DO NOTHING;

-- 2d. Patient "OutboxMessages" (auto-confirmed events for seed data)
INSERT INTO "OutboxMessages" (Id, Type, Content, CorrelationId, CausationId, OccurredOn, ProcessedOn, Status, Error, RetryCount) VALUES
('00000000-0000-0000-0000-000000000071', 'PatientCreated', '{"patientId":"00000000-0000-0000-0000-000000000001","fullName":"Nguyễn Văn A"}', 'corr-patient-001', 'caus-patient-001', '2026-07-01 08:00:00+00', '2026-07-01 08:00:05+00', 'Processed', NULL, 0),
('00000000-0000-0000-0000-000000000072', 'PatientCreated', '{"patientId":"00000000-0000-0000-0000-000000000002","fullName":"Trần Thị B"}', 'corr-patient-002', 'caus-patient-002', '2026-07-01 08:00:00+00', '2026-07-01 08:00:05+00', 'Processed', NULL, 0),
('00000000-0000-0000-0000-000000000073', 'PatientCreated', '{"patientId":"00000000-0000-0000-0000-000000000003","fullName":"Lê Văn C"}', 'corr-patient-003', 'caus-patient-003', '2026-07-01 08:00:00+00', '2026-07-01 08:00:05+00', 'Processed', NULL, 0),
('00000000-0000-0000-0000-000000000074', 'PatientCreated', '{"patientId":"00000000-0000-0000-0000-000000000004","fullName":"Phạm Thị D"}', 'corr-patient-004', 'caus-patient-004', '2026-07-01 08:00:00+00', '2026-07-01 08:00:05+00', 'Processed', NULL, 0),
('00000000-0000-0000-0000-000000000075', 'PatientCreated', '{"patientId":"00000000-0000-0000-0000-000000000005","fullName":"Hoàng Văn E"}', 'corr-patient-005', 'caus-patient-005', '2026-07-01 08:00:00+00', '2026-07-01 08:00:05+00', 'Processed', NULL, 0),
('00000000-0000-0000-0000-000000000076', 'PatientCreated', '{"patientId":"00000000-0000-0000-0000-000000000006","fullName":"Vũ Thị F"}', 'corr-patient-006', 'caus-patient-006', '2026-07-01 08:00:00+00', '2026-07-01 08:00:05+00', 'Processed', NULL, 0),
('00000000-0000-0000-0000-000000000077', 'PatientCreated', '{"patientId":"00000000-0000-0000-0000-000000000007","fullName":"Đặng Văn G"}', 'corr-patient-007', 'caus-patient-007', '2026-07-01 08:00:00+00', '2026-07-01 08:00:05+00', 'Processed', NULL, 0),
('00000000-0000-0000-0000-000000000078', 'PatientCreated', '{"patientId":"00000000-0000-0000-0000-000000000008","fullName":"Bùi Thị H"}', 'corr-patient-008', 'caus-patient-008', '2026-07-01 08:00:00+00', '2026-07-01 08:00:05+00', 'Processed', NULL, 0)
ON CONFLICT (Id) DO NOTHING;

-- ============================================================================
-- SECTION 3: APPOINTMENT SERVICE (appointmentdb)
-- ============================================================================

INSERT INTO "Appointments" (AppointmentId, PatientId, ProviderId, FacilityId, ScheduledDate, DurationMinutes, Status, Reason, Notes, CheckInAt, CheckOutAt, CanceledAt, CancelReason, CreatedAt, UpdatedAt) VALUES
-- A001: Nguyễn Văn A với BS Nguyễn - hôm nay 08:00 - Đã check-in
('00000000-0000-0000-0000-000000000201', '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000102', '11111111-1111-1111-1111-111111111111', '2026-07-15 08:00:00+00', 30, 'CheckedIn', 'Tái khám tăng huyết áp - đau đầu, chóng mặt', 'Bệnh nhân tái khám định kỳ', '2026-07-15 07:55:00+00', NULL, NULL, NULL, '2026-07-10 09:00:00+00', '2026-07-15 07:55:00+00'),
-- A002: Trần Thị B với BS Trần - hôm nay 09:00 - Đã check-in
('00000000-0000-0000-0000-000000000202', '00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000103', '11111111-1111-1111-1111-111111111111', '2026-07-15 09:00:00+00', 30, 'CheckedIn', 'Khám sức khỏe tổng quát', 'Khám định kỳ theo yêu cầu công ty', '2026-07-15 08:50:00+00', NULL, NULL, NULL, '2026-07-08 10:00:00+00', '2026-07-15 08:50:00+00'),
-- A003: Lê Văn C với BS Nguyễn - hôm nay 10:00 - Đang khám
('00000000-0000-0000-0000-000000000203', '00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000102', '11111111-1111-1111-1111-111111111111', '2026-07-15 10:00:00+00', 45, 'InProgress', 'Đau thượng vị, ợ nóng, đầy bụng sau ăn', 'Bệnh nhân lớn tuổi, nhiều bệnh nền', '2026-07-15 09:55:00+00', NULL, NULL, NULL, '2026-07-09 11:00:00+00', '2026-07-15 09:55:00+00'),
-- A004: Phạm Thị D với BS Trần - ngày mai 08:00
('00000000-0000-0000-0000-000000000204', '00000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000103', '11111111-1111-1111-1111-111111111111', '2026-07-16 08:00:00+00', 30, 'Scheduled', 'Khó thở, ho, nặng ngực - hen suyễn', 'Cơn hen cấp, cần khám sớm', NULL, NULL, NULL, NULL, '2026-07-11 14:00:00+00', NULL),
-- A005: Hoàng Văn E với BS Nguyễn - ngày mai 09:30 (nhi khoa)
('00000000-0000-0000-0000-000000000205', '00000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000102', '11111111-1111-1111-1111-111111111111', '2026-07-16 09:30:00+00', 30, 'Scheduled', 'Sốt cao, ho, sổ mũi', 'Bệnh nhi 5 tuổi, cần theo dõi sát', NULL, NULL, NULL, NULL, '2026-07-12 08:00:00+00', NULL),
-- A006: Vũ Thị F với BS Nguyễn - ngày kia 14:00
('00000000-0000-0000-0000-000000000206', '00000000-0000-0000-0000-000000000006', '00000000-0000-0000-0000-000000000102', '11111111-1111-1111-1111-111111111111', '2026-07-17 14:00:00+00', 30, 'Scheduled', 'Kiểm tra đường huyết định kỳ', 'Xét nghiệm HbA1c theo dõi đái tháo đường', NULL, NULL, NULL, NULL, '2026-07-13 09:00:00+00', NULL),
-- A007: Đặng Văn G với BS Trần - hôm nay 15:00 - Đã hoàn thành
('00000000-0000-0000-0000-000000000207', '00000000-0000-0000-0000-000000000007', '00000000-0000-0000-0000-000000000103', '11111111-1111-1111-1111-111111111111', '2026-07-15 15:00:00+00', 45, 'Completed', 'Khám sức khỏe tổng quát định kỳ', 'Kết quả xét nghiệm mỡ máu cao', '2026-07-15 14:55:00+00', '2026-07-15 15:40:00+00', NULL, NULL, '2026-07-10 08:00:00+00', '2026-07-15 15:40:00+00'),
-- A008: Bùi Thị H với BS Nguyễn - tuần sau 08:00
('00000000-0000-0000-0000-000000000208', '00000000-0000-0000-0000-000000000008', '00000000-0000-0000-0000-000000000102', '11111111-1111-1111-1111-111111111111', '2026-07-22 08:00:00+00', 30, 'Scheduled', 'Tái khám tăng huyết áp và đái tháo đường', 'Bệnh nhân cao tuổi, cần khám tổng thể', NULL, NULL, NULL, NULL, '2026-07-14 10:00:00+00', NULL),
-- A009: Trần Thị B với BS Trần - tuần sau 10:00
('00000000-0000-0000-0000-000000000209', '00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000103', '11111111-1111-1111-1111-111111111111', '2026-07-22 10:00:00+00', 30, 'Scheduled', 'Tái khám kết quả xét nghiệm', 'Hẹn nhận kết quả xét nghiệm máu', NULL, NULL, NULL, NULL, '2026-07-14 14:00:00+00', NULL),
-- A010: Nguyễn Văn A với BS Trần - 18/07 - Đã hủy
('00000000-0000-0000-0000-000000000210', '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000103', '11111111-1111-1111-1111-111111111111', '2026-07-18 07:30:00+00', 30, 'Cancelled', 'Tái khám định kỳ', NULL, NULL, NULL, '2026-07-14 16:00:00+00', 'Bệnh nhân bận việc đột xuất', '2026-07-12 11:00:00+00', '2026-07-14 16:00:00+00')
ON CONFLICT (AppointmentId) DO NOTHING;

-- Appointment "OutboxMessages"
INSERT INTO "OutboxMessages" (Id, Type, Content, CorrelationId, CausationId, OccurredOn, ProcessedOn, Status) VALUES
('00000000-0000-0000-0000-000000000251', 'AppointmentCreated', '{"appointmentId":"00000000-0000-0000-0000-000000000201","patientId":"00000000-0000-0000-0000-000000000001"}', 'corr-appt-201', 'caus-appt-201', '2026-07-10 09:00:00+00', '2026-07-10 09:00:05+00', 'Processed'),
('00000000-0000-0000-0000-000000000252', 'AppointmentCreated', '{"appointmentId":"00000000-0000-0000-0000-000000000202","patientId":"00000000-0000-0000-0000-000000000002"}', 'corr-appt-202', 'caus-appt-202', '2026-07-08 10:00:00+00', '2026-07-08 10:00:05+00', 'Processed'),
('00000000-0000-0000-0000-000000000253', 'AppointmentCreated', '{"appointmentId":"00000000-0000-0000-0000-000000000203","patientId":"00000000-0000-0000-0000-000000000003"}', 'corr-appt-203', 'caus-appt-203', '2026-07-09 11:00:00+00', '2026-07-09 11:00:05+00', 'Processed'),
('00000000-0000-0000-0000-000000000257', 'AppointmentCompleted', '{"appointmentId":"00000000-0000-0000-0000-000000000207","patientId":"00000000-0000-0000-0000-000000000007"}', 'corr-appt-207', 'caus-appt-207', '2026-07-15 15:40:00+00', '2026-07-15 15:40:05+00', 'Processed'),
('00000000-0000-0000-0000-000000000260', 'AppointmentCancelled', '{"appointmentId":"00000000-0000-0000-0000-000000000210","patientId":"00000000-0000-0000-0000-000000000001","reason":"Bệnh nhân bận việc đột xuất"}', 'corr-appt-210', 'caus-appt-210', '2026-07-14 16:00:00+00', '2026-07-14 16:00:05+00', 'Processed')
ON CONFLICT (Id) DO NOTHING;

-- ============================================================================
-- SECTION 4: CLINICAL SERVICE (clinicaldb)
-- ============================================================================

-- 4a. "Encounters" (6 clinical "Encounters" with SOAP notes in Vietnamese)
INSERT INTO "Encounters" (EncounterId, PatientId, AppointmentId, ProviderId, EncounterDate, EncounterType, Status, ChiefComplaint,
    HpiOnset, HpiLocation, HpiDuration, HpiCharacteristics, HpiAggravatingFactors, HpiRelievingFactors, HpiPriorTreatments,
    Temperature, HeartRate, RespiratoryRate, SystolicBP, DiastolicBP, OxygenSaturation, HeightCm, WeightKg, Bmi,
    Assessment, Plan, DiagnosisNotes, CreatedAt, UpdatedAt) VALUES

-- E001: Nguyễn Văn A - Hypertension follow-up (linked to A001)
('00000000-0000-0000-0000-000000000301', '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000201', '00000000-0000-0000-0000-000000000102',
 '2026-07-15 08:05:00+00', 'FollowUp', 'IN_PROGRESS',
 'Đau đầu vùng chẩm, chóng mặt khi thay đổi tư thế, cảm giác hồi hộp',
 'Cách đây 3 ngày', 'Vùng chẩm lan ra hai bên thái dương', '3 ngày',
 'Đau đầu kiểu căng thẳng, chóng mặt quay cuồng khi đứng dậy đột ngột', 'Khi căng thẳng, thiếu ngủ', 'Nghỉ ngơi giảm đau đầu',
 'Đang dùng Amlodipine 5mg/ngày do bác sĩ Bình kê từ tháng 3/2026',
 37.2, 88, 18, 155, 95, 98.0, 168.0, 72.0, 25.5,
 'Bệnh nhân tăng huyết áp giai đoạn 2 chưa kiểm soát tốt.
HA hiện tại 155/95 mmHg.
Cần điều chỉnh thuốc: bổ sung Losartan kết hợp Amlodipine.
Nguy cơ tim mạch cao (có kèm ĐTĐ type 2).
Khuyến cáo theo dõi HA tại nhà, ghi nhật ký huyết áp.',
 '1. Bổ sung Losartan 50mg x 1 lần/ngày
2. Tiếp tục Amlodipine 5mg x 1 lần/ngày
3. Xét nghiệm: CBC, Lipid Panel, HbA1c, Urinalysis
4. Theo dõi HA tại nhà 2 lần/ngày
5. Tái khám sau 2 tuần
6. Chế độ ăn giảm muối, giảm mỡ',
 'Tăng huyết áp giai đoạn 2 (I10) - chưa kiểm soát
Đái tháo đường type 2 (E11) - ổn định',
 '2026-07-15 08:05:00+00', '2026-07-15 08:30:00+00'),

-- E002: Hoàng Văn E (pediatric) - Sốt cao (linked to A005)
('00000000-0000-0000-0000-000000000302', '00000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000205', '00000000-0000-0000-0000-000000000102',
 '2026-07-16 09:30:00+00', 'Consultation', 'IN_PROGRESS',
 'Sốt cao 39.5°C, ho khan, sổ mũi trong 2 ngày. Chán ăn, quấy khóc.',
 '2 ngày trước', 'Toàn thân', '2 ngày',
 'Sốt cao liên tục 38.5-39.5°C, ho khan từng cơn, chảy nước mũi trong', 'Về đêm sốt cao hơn, ho nhiều hơn', 'Paracetamol 250mg hạ sốt tạm thời',
 'Mẹ cho uống Paracetamol 250mg mỗi 6h, sốt có giảm nhưng tái phát',
 39.1, 120, 26, 95, 60, 97.0, 110.0, 18.5, 15.3,
 'Bệnh nhi 5 tuổi, sốt cao do viêm hô hấp trên cấp.
Khám họng: niêm mạc họng đỏ, amidan không sưng.
Phổi: nghe thấy ran rải rác hai bên.
Chẩn đoán: Viêm hô hấp trên cấp do virus.
Tiên lượng tốt, cần theo dõi sát nhiệt độ.',
 '1. Hạ sốt: Paracetamol 250mg mỗi 6h khi sốt >38.5°C
2. Vệ sinh mũi bằng nước muối sinh lý
3. Bù nước: Oresol theo nhu cầu
4. Theo dõi nhiệt độ mỗi 4h
5. Nhập viện nếu sốt cao liên tục >40°C hoặc co giật
6. Tái khám sau 3 ngày nếu không đỡ',
 'Viêm hô hấp trên cấp (J06.9)',
 '2026-07-16 09:30:00+00', NULL),

-- E003: Vũ Thị F - Diabetes checkup (linked to A006)
('00000000-0000-0000-0000-000000000303', '00000000-0000-0000-0000-000000000006', '00000000-0000-0000-0000-000000000206', '00000000-0000-0000-0000-000000000102',
 '2026-07-17 14:00:00+00', 'FollowUp', 'SCHEDULED',
 'Kiểm tra đường huyết định kỳ. Bệnh nhân ĐTĐ type 2, tái khám theo hẹn.',
 'Đã theo dõi 3 tháng', 'Toàn thân', '3 tháng',
 'Đường huyết mao mạch sáng đói: 7.8-9.2 mmol/L (cao hơn mục tiêu <7.0)
Bệnh nhân tuân thủ điều trị khá tốt', 'Khi ăn nhiều tinh bột', 'Metformin đều đặn',
 'Đang dùng Metformin 850mg x 2 lần/ngày. Có chế độ ăn kiêng.',
 36.8, 76, 16, 130, 80, 98.5, 160.0, 68.0, 26.6,
 'Đái tháo đường type 2 chưa kiểm soát tối ưu.
HbA1c dự kiến >7.5%.
Cần đánh giá HbA1c, điều chỉnh thuốc nếu cần.
Bệnh nhân kiểm soát ăn uống chưa tốt.
Cần tư vấn chế độ dinh dưỡng chi tiết.',
 '1. Xét nghiệm: HbA1c, đường huyết đói, Lipid Panel
2. Tiếp tục Metformin 850mg x 2 lần/ngày
3. Khám dinh dưỡng - tư vấn chế độ ăn
4. Tập thể dục 30 phút/ngày
5. Tái khám sau 1 tháng có kết quả HbA1c',
 'Đái tháo đường type 2 (E11) - kiểm soát chưa tối ưu',
 '2026-07-17 14:00:00+00', NULL),

-- E004: Phạm Thị D - Asthma exacerbation (linked to A004)
('00000000-0000-0000-0000-000000000304', '00000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000204', '00000000-0000-0000-0000-000000000103',
 '2026-07-16 08:00:00+00', 'Consultation', 'SCHEDULED',
 'Khó thở từng cơn, ho nhiều về đêm, nặng ngực. Đã 3 ngày nay.',
 '3 ngày trước sau khi tiếp xúc lạnh', 'Ngực, khó thở', '3 ngày',
 'Khó thở từng cơn, thở khò khè, ho khan về đêm, nặng ngực.
Không sốt. Đờm trắng trong ít.', 'Không khí lạnh, gắng sức, về đêm', 'Xịt Salbutamol giảm tạm thời',
 'Xịt Salbutamol 2 nhát mỗi 6h, đỡ nhưng tái phát.
Không dùng thuốc dự phòng thường xuyên.',
 37.0, 98, 24, 115, 75, 95.5, 162.0, 55.0, 20.9,
 'Cơn hen cấp mức độ trung bình.
Nghe phổi: ran rít, ran ngáy hai bên.
Nhịp thở 24 lần/phút. SpO2 95.5%.
Đáp ứng kém với Salbutamol đơn thuần.
Chẩn đoán: Hen phế quản cơn cấp trung bình.
Cần corticoid đường uống ngắn ngày.',
 '1. Salbutamol 100mcg xịt 2 nhát mỗi 4h khi còn khó thở
2. Prednisolone 40mg/ngày x 5 ngày
3. Đo chức năng hô hấp
4. Tái khám sau 1 tuần
5. Nếu nặng hơn: nhập viện',
 'Hen phế quản cơn cấp (J45.9)',
 '2026-07-16 08:00:00+00', NULL),

-- E005: Lê Văn C - Abdominal pain (linked to A003)
('00000000-0000-0000-0000-000000000305', '00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000203', '00000000-0000-0000-0000-000000000102',
 '2026-07-15 10:00:00+00', 'Consultation', 'IN_PROGRESS',
 'Đau thượng vị, ợ nóng, ợ chua, đầy bụng sau ăn khoảng 2 tháng nay.',
 '2 tháng', 'Thượng vị', '2 tháng',
 'Đau nóng rát vùng thượng vị, ợ chua, nhất là sau ăn no và về đêm.
Có cảm giác buồn nôn, không nôn. Ăn uống kém hơn.
Gần đây hay đầy bụng, khó tiêu.', 'Ăn đồ cay nóng, dầu mỡ, nằm sau ăn', 'Uống thuốc dạ dày (không rõ loại) đỡ tạm',
 'Mua thuốc dạ dày không kê đơn uống nhiều đợt, đỡ tạm thời.
Chưa nội soi dạ dày bao giờ.',
 36.9, 82, 18, 145, 88, 97.5, 170.0, 75.0, 26.0,
 'Viêm dạ dày - trào ngược dạ dày thực quản (GERD).
Bệnh nhân nam 67 tuổi, có tiền sử tăng huyết áp, COPD.
Cần nội soi dạ dày để đánh giá mức độ tổn thương.
Loét dạ dày chưa loại trừ. Cần loại trừ H.pylori.',
 '1. Pantoprazole 40mg/ngày x 8 tuần
2. Nội soi dạ dày có sinh thiết H.pylori
3. Xét nghiệm: công thức máu, chức năng gan
4. Chế độ ăn: chia nhỏ bữa, tránh đồ cay nóng, dầu mỡ
5. Nâng cao đầu giường khi ngủ
6. Tái khám sau 1 tuần có kết quả nội soi',
 'Viêm dạ dày (K29.5)
Trào ngược dạ dày thực quản (K21.9)',
 '2026-07-15 10:00:00+00', NULL),

-- E006: Đặng Văn G - Annual health check (linked to A007, completed)
('00000000-0000-0000-0000-000000000306', '00000000-0000-0000-0000-000000000007', '00000000-0000-0000-0000-000000000207', '00000000-0000-0000-0000-000000000103',
 '2026-07-15 15:00:00+00', 'Consultation', 'COMPLETED',
 'Khám sức khỏe tổng quát định kỳ năm 2026. Bệnh nhân không có triệu chứng bất thường.',
 'Không có - khám định kỳ', 'Toàn thân', 'Không',
 'Sức khỏe tổng quát tốt. Không đau ốm gì đặc biệt.
Có tiền sử mỡ máu cao phát hiện năm 2025.
Chưa điều trị gì.', 'Không', 'Không',
 'Chưa dùng thuốc gì thường xuyên. Có tập thể dục 2-3 lần/tuần.',
 36.7, 72, 16, 125, 80, 99.0, 172.0, 80.0, 27.0,
 'Sức khỏe tổng quát tốt, không phát hiện bất thường lâm sàng.
Kết quả xét nghiệm: mỡ máu cao (LDL 4.2 mmol/L, Triglyceride 3.1 mmol/L).
BMI 27 - thừa cân nhẹ.
Cần điều chỉnh chế độ ăn và dùng thuốc hạ mỡ máu.',
 '1. Atorvastatin 10mg/ngày x 3 tháng, tái khám kiểm tra lại lipid
2. Chế độ ăn giảm mỡ, tăng rau xanh
3. Tập thể dục 30 phút/ngày, 5 ngày/tuần
4. Kiểm tra chức năng gan sau 1 tháng dùng Statin
5. Tái khám sau 3 tháng',
 'Tăng cholesterol máu (E78.0)
Thừa cân (R63.5) - BMI 27',
 '2026-07-15 15:00:00+00', '2026-07-15 15:40:00+00')
ON CONFLICT (EncounterId) DO NOTHING;

-- 4b. "EncounterDiagnoses" (child table of "Encounters")
INSERT INTO "EncounterDiagnoses" (EncounterId, Id, ConditionName, Icd10Code, IsPrimary, Notes) VALUES
-- E001: Nguyễn Văn A
('00000000-0000-0000-0000-000000000301', 1, 'Tăng huyết áp giai đoạn 2', 'I10', true, 'HA 155/95 mmHg, chưa kiểm soát với Amlodipine đơn trị'),
('00000000-0000-0000-0000-000000000301', 2, 'Đái tháo đường type 2', 'E11', false, 'Kiểm soát trung bình, cần theo dõi HbA1c'),
-- E002: Hoàng Văn E - pediatric
('00000000-0000-0000-0000-000000000302', 1, 'Viêm hô hấp trên cấp', 'J06.9', true, 'Sốt cao 39.5°C, ho, sổ mũi, nhi khoa'),
-- E003: Vũ Thị F
('00000000-0000-0000-0000-000000000303', 1, 'Đái tháo đường type 2 kiểm soát chưa tối ưu', 'E11', true, 'Đường huyết đói 7.8-9.2 mmol/L, cần đánh giá HbA1c'),
-- E004: Phạm Thị D
('00000000-0000-0000-0000-000000000304', 1, 'Hen phế quản cơn cấp trung bình', 'J45.9', true, 'Khó thở, SpO2 95.5%, đáp ứng kém Salbutamol đơn thuần'),
-- E005: Lê Văn C
('00000000-0000-0000-0000-000000000305', 1, 'Viêm dạ dày cấp', 'K29.5', true, 'Đau thượng vị, ợ nóng, cần nội soi kiểm tra'),
('00000000-0000-0000-0000-000000000305', 2, 'Trào ngược dạ dày thực quản', 'K21.9', false, 'Triệu chứng GERD điển hình'),
-- E006: Đặng Văn G
('00000000-0000-0000-0000-000000000306', 1, 'Tăng cholesterol máu', 'E78.0', true, 'LDL 4.2 mmol/L, Triglyceride 3.1 mmol/L'),
('00000000-0000-0000-0000-000000000306', 2, 'Thừa cân', 'R63.5', false, 'BMI 27')
ON CONFLICT (EncounterId, Id) DO NOTHING;

-- 4c. "EncounterProcedures" (child table of "Encounters")
INSERT INTO "EncounterProcedures" (EncounterId, Id, ProcedureName, CptCode, PerformedDate, Notes) VALUES
-- E001: Đo HA, điện tâm đồ
('00000000-0000-0000-0000-000000000301', 1, 'Đo huyết áp động mạch', '99214', '2026-07-15 08:10:00+00', 'HA 155/95 mmHg'),
('00000000-0000-0000-0000-000000000301', 2, 'Điện tâm đồ', '93000', '2026-07-15 08:15:00+00', 'Nhịp xoang đều, không thiếu máu cơ tim'),
-- E002: Khám nhi
('00000000-0000-0000-0000-000000000302', 1, 'Khám nhi khoa', '99382', '2026-07-16 09:30:00+00', 'Khám toàn diện bệnh nhi 5 tuổi'),
-- E004: Đo chức năng hô hấp
('00000000-0000-0000-0000-000000000304', 1, 'Đo chức năng hô hấp', '94010', '2026-07-16 08:15:00+00', 'FEV1 65% dự đoán, FEV1/FVC <0.7'),
-- E005: Tư vấn nội soi
('00000000-0000-0000-0000-000000000305', 1, 'Tư vấn nội soi dạ dày', '99244', '2026-07-15 10:15:00+00', 'Hẹn nội soi trong tuần'),
-- E006: Khám tổng quát
('00000000-0000-0000-0000-000000000306', 1, 'Khám sức khỏe tổng quát', '99396', '2026-07-15 15:05:00+00', 'Khám định kỳ năm')
ON CONFLICT (EncounterId, Id) DO NOTHING;

-- 4d. Clinical Notes (Progress notes in Vietnamese)
INSERT INTO "ClinicalNotes" (EncounterId, NoteId, NoteType, Content, RecordedAt, RecordedBy) VALUES
-- E001: Progress note - hypertension
('00000000-0000-0000-0000-000000000301', '00000000-0000-0000-0000-000000000311', 'Progress',
 'Bệnh nhân đỡ đau đầu hơn sau khi dùng thuốc.
Huyết áp đo lại: 148/90 mmHg.
Đã giải thích cho bệnh nhân về phác đồ điều trị mới.
Bệnh nhân đồng ý tuân thủ điều trị.
Đã lấy máu xét nghiệm.
Hẹn tái khám sau 2 tuần.',
 '2026-07-15 08:45:00+00', '00000000-0000-0000-0000-000000000102'),

-- E004: Progress note - asthma
('00000000-0000-0000-0000-000000000304', '00000000-0000-0000-0000-000000000312', 'Progress',
 'Bệnh nhân nữ, 28 tuổi, vào khám vì khó thở từng cơn.
Đã hướng dẫn sử dụng Salbutamol đúng cách.
Kê đơn Prednisolone 40mg x 5 ngày.
Bệnh nhân đã được tư vấn về phòng tránh dị nguyên.
Hẹn tái khám sau 1 tuần hoặc sớm hơn nếu triệu chứng nặng.',
 '2026-07-16 08:30:00+00', '00000000-0000-0000-0000-000000000103'),

-- E002: Progress note - pediatric fever
('00000000-0000-0000-0000-000000000302', '00000000-0000-0000-0000-000000000313', 'Progress',
 'Bệnh nhi 5 tuổi, sốt cao 39.5°C.
Đã khám và chẩn đoán viêm hô hấp trên cấp.
Hướng dẫn phụ huynh cách chăm sóc và theo dõi nhiệt độ.
Kê toa Paracetamol 250mg và nước muối sinh lý.
Dặn tái khám ngay nếu sốt cao liên tục >40°C, co giật, hoặc khó thở.',
 '2026-07-16 10:00:00+00', '00000000-0000-0000-0000-000000000102')
ON CONFLICT (EncounterId, NoteId) DO NOTHING;

-- 4e. Clinical "Prescriptions" (linked to "Encounters")
INSERT INTO "Prescriptions" (EncounterId, PrescriptionId, MedicationName, Dosage, Frequency, Route, DurationDays, PrescribedAt, PrescribedBy, Instructions, IsActive) VALUES
-- E001: Amlodipine + Losartan for Nguyễn Văn A
('00000000-0000-0000-0000-000000000301', '00000000-0000-0000-0000-000000000321', 'Amlodipine 5mg', '1 viên (5mg)', '1 lần/ngày - sáng', 'Uống', 30, '2026-07-15 08:30:00+00', '00000000-0000-0000-0000-000000000102', 'Uống vào buổi sáng, không nhai', true),
('00000000-0000-0000-0000-000000000301', '00000000-0000-0000-0000-000000000322', 'Losartan 50mg', '1 viên (50mg)', '1 lần/ngày - tối', 'Uống', 30, '2026-07-15 08:30:00+00', '00000000-0000-0000-0000-000000000102', 'Uống vào buổi tối trước khi đi ngủ', true),
-- E005: Pantoprazole for Lê Văn C
('00000000-0000-0000-0000-000000000305', '00000000-0000-0000-0000-000000000323', 'Pantoprazole 40mg', '1 viên (40mg)', '1 lần/ngày - sáng trước ăn', 'Uống', 56, '2026-07-15 10:30:00+00', '00000000-0000-0000-0000-000000000102', 'Uống trước ăn sáng 30 phút, uống nguyên viên', true),
-- E006: Atorvastatin for Đặng Văn G
('00000000-0000-0000-0000-000000000306', '00000000-0000-0000-0000-000000000324', 'Atorvastatin 10mg', '1 viên (10mg)', '1 lần/ngày - tối', 'Uống', 90, '2026-07-15 15:30:00+00', '00000000-0000-0000-0000-000000000103', 'Uống vào buổi tối, kiểm tra chức năng gan sau 1 tháng', true)
ON CONFLICT (EncounterId, PrescriptionId) DO NOTHING;

-- Clinical "OutboxMessages"
INSERT INTO "OutboxMessages" (Id, Type, Content, CorrelationId, OccurredOn, ProcessedOn, Status) VALUES
('00000000-0000-0000-0000-000000000341', 'EncounterCreated', '{"encounterId":"00000000-0000-0000-0000-000000000301","patientId":"00000000-0000-0000-0000-000000000001"}', 'corr-enc-301', '2026-07-15 08:05:00+00', '2026-07-15 08:05:05+00', 'Processed'),
('00000000-0000-0000-0000-000000000342', 'EncounterCreated', '{"encounterId":"00000000-0000-0000-0000-000000000302","patientId":"00000000-0000-0000-0000-000000000005"}', 'corr-enc-302', '2026-07-16 09:30:00+00', NULL, 'Pending'),
('00000000-0000-0000-0000-000000000343', 'EncounterCreated', '{"encounterId":"00000000-0000-0000-0000-000000000303","patientId":"00000000-0000-0000-0000-000000000006"}', 'corr-enc-303', '2026-07-17 14:00:00+00', NULL, 'Pending'),
('00000000-0000-0000-0000-000000000344', 'EncounterCreated', '{"encounterId":"00000000-0000-0000-0000-000000000304","patientId":"00000000-0000-0000-0000-000000000004"}', 'corr-enc-304', '2026-07-16 08:00:00+00', NULL, 'Pending'),
('00000000-0000-0000-0000-000000000345', 'EncounterCreated', '{"encounterId":"00000000-0000-0000-0000-000000000305","patientId":"00000000-0000-0000-0000-000000000003"}', 'corr-enc-305', '2026-07-15 10:00:00+00', '2026-07-15 10:00:05+00', 'Processed'),
('00000000-0000-0000-0000-000000000346', 'EncounterCreated', '{"encounterId":"00000000-0000-0000-0000-000000000306","patientId":"00000000-0000-0000-0000-000000000007"}', 'corr-enc-306', '2026-07-15 15:00:00+00', '2026-07-15 15:00:05+00', 'Processed')
ON CONFLICT (Id) DO NOTHING;

-- ============================================================================
-- SECTION 5: PHARMACY SERVICE (his_hope_pharmacy)
-- ============================================================================

-- 5a. "Medications" (12 items common in Vietnamese practice)
INSERT INTO "Medications" (Id, Name, GenericName, BrandName, DosageForm, Strength, Route, RequiresPrescription, IsActive, CreatedAt, UpdatedAt) VALUES
('00000000-0000-0000-0000-000000000601', 'Paracetamol 500mg', 'Paracetamol', 'Panadol', 'Viên nén', '500mg', 'Uống', false, true, '2026-01-01 08:00:00+00', NULL),
('00000000-0000-0000-0000-000000000602', 'Amoxicillin 250mg', 'Amoxicillin', 'Amoxil', 'Viên nang', '250mg', 'Uống', true, true, '2026-01-01 08:00:00+00', NULL),
('00000000-0000-0000-0000-000000000603', 'Omeprazole 20mg', 'Omeprazole', 'Losec', 'Viên nang', '20mg', 'Uống', true, true, '2026-01-01 08:00:00+00', NULL),
('00000000-0000-0000-0000-000000000604', 'Metformin 850mg', 'Metformin', 'Glucophage', 'Viên nén', '850mg', 'Uống', true, true, '2026-01-01 08:00:00+00', NULL),
('00000000-0000-0000-0000-000000000605', 'Amlodipine 5mg', 'Amlodipine', 'Norvasc', 'Viên nén', '5mg', 'Uống', true, true, '2026-01-01 08:00:00+00', NULL),
('00000000-0000-0000-0000-000000000606', 'Atorvastatin 10mg', 'Atorvastatin', 'Lipitor', 'Viên nén', '10mg', 'Uống', true, true, '2026-01-01 08:00:00+00', NULL),
('00000000-0000-0000-0000-000000000607', 'Cetirizine 10mg', 'Cetirizine', 'Zyrtec', 'Viên nén', '10mg', 'Uống', false, true, '2026-01-01 08:00:00+00', NULL),
('00000000-0000-0000-0000-000000000608', 'Salbutamol 100mcg', 'Salbutamol', 'Ventolin', 'Bình xịt định liều', '100mcg', 'Hít', true, true, '2026-01-01 08:00:00+00', NULL),
('00000000-0000-0000-0000-000000000609', 'Pantoprazole 40mg', 'Pantoprazole', 'Controloc', 'Viên nén', '40mg', 'Uống', true, true, '2026-01-01 08:00:00+00', NULL),
('00000000-0000-0000-0000-000000000610', 'Losartan 50mg', 'Losartan', 'Cozaar', 'Viên nén', '50mg', 'Uống', true, true, '2026-01-01 08:00:00+00', NULL),
('00000000-0000-0000-0000-000000000611', 'Ibuprofen 400mg', 'Ibuprofen', 'Brufen', 'Viên nén', '400mg', 'Uống', false, true, '2026-01-01 08:00:00+00', NULL),
('00000000-0000-0000-0000-000000000612', 'Ciprofloxacin 500mg', 'Ciprofloxacin', 'Ciprobay', 'Viên nén', '500mg', 'Uống', true, true, '2026-01-01 08:00:00+00', NULL)
ON CONFLICT (Id) DO NOTHING;

-- 5b. Pharmacy "Prescriptions" (independent of clinical "Encounters")
INSERT INTO "Prescriptions" (Id, PatientId, ProviderId, MedicationId, MedicationName, Strength, DosageForm, DosageInstructions, Route, Quantity, Refills, Notes, Status, PrescribedDate, ExpiryDate, FilledDate, CreatedAt, UpdatedAt) VALUES
-- Rx001: Nguyễn Văn A - Amlodipine 5mg
('00000000-0000-0000-0000-000000000631', '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000102', '00000000-0000-0000-0000-000000000605', 'Amlodipine 5mg', '5mg', 'Viên nén', 'Uống 1 viên mỗi sáng', 'Uống', 30, 2, 'Điều trị tăng huyết áp', 'ACTIVE', '2026-07-15 08:30:00+00', '2026-10-15 08:30:00+00', NULL, '2026-07-15 08:30:00+00', NULL),
-- Rx002: Nguyễn Văn A - Losartan 50mg
('00000000-0000-0000-0000-000000000632', '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000102', '00000000-0000-0000-0000-000000000610', 'Losartan 50mg', '50mg', 'Viên nén', 'Uống 1 viên mỗi tối', 'Uống', 30, 2, 'Bổ sung phác đồ điều trị tăng huyết áp', 'ACTIVE', '2026-07-15 08:30:00+00', '2026-10-15 08:30:00+00', NULL, '2026-07-15 08:30:00+00', NULL),
-- Rx003: Lê Văn C - Pantoprazole 40mg
('00000000-0000-0000-0000-000000000633', '00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000102', '00000000-0000-0000-0000-000000000609', 'Pantoprazole 40mg', '40mg', 'Viên nén', 'Uống 1 viên trước ăn sáng 30 phút', 'Uống', 56, 1, 'Điều trị viêm dạ dày - GERD', 'ACTIVE', '2026-07-15 10:30:00+00', '2026-09-15 10:30:00+00', NULL, '2026-07-15 10:30:00+00', NULL),
-- Rx004: Đặng Văn G - Atorvastatin 10mg (đã phát thuốc)
('00000000-0000-0000-0000-000000000634', '00000000-0000-0000-0000-000000000007', '00000000-0000-0000-0000-000000000103', '00000000-0000-0000-0000-000000000606', 'Atorvastatin 10mg', '10mg', 'Viên nén', 'Uống 1 viên mỗi tối. Kiểm tra chức năng gan sau 1 tháng.', 'Uống', 90, 2, 'Điều trị tăng cholesterol máu', 'FILLED', '2026-07-15 15:30:00+00', '2026-10-15 15:30:00+00', '2026-07-15 16:00:00+00', '2026-07-15 15:30:00+00', '2026-07-15 16:00:00+00')
ON CONFLICT (Id) DO NOTHING;

-- Pharmacy "OutboxMessages"
INSERT INTO "OutboxMessages" (Id, Type, Content, CorrelationId, OccurredOn, ProcessedOn, Status) VALUES
('00000000-0000-0000-0000-000000000641', 'PrescriptionCreated', '{"prescriptionId":"00000000-0000-0000-0000-000000000631","patientId":"00000000-0000-0000-0000-000000000001","medication":"Amlodipine 5mg"}', 'corr-rx-631', '2026-07-15 08:30:00+00', '2026-07-15 08:30:05+00', 'Processed'),
('00000000-0000-0000-0000-000000000642', 'PrescriptionCreated', '{"prescriptionId":"00000000-0000-0000-0000-000000000632","patientId":"00000000-0000-0000-0000-000000000001","medication":"Losartan 50mg"}', 'corr-rx-632', '2026-07-15 08:30:00+00', '2026-07-15 08:30:05+00', 'Processed'),
('00000000-0000-0000-0000-000000000643', 'PrescriptionCreated', '{"prescriptionId":"00000000-0000-0000-0000-000000000633","patientId":"00000000-0000-0000-0000-000000000003","medication":"Pantoprazole 40mg"}', 'corr-rx-633', '2026-07-15 10:30:00+00', '2026-07-15 10:30:05+00', 'Processed'),
('00000000-0000-0000-0000-000000000644', 'PrescriptionFilled', '{"prescriptionId":"00000000-0000-0000-0000-000000000634","patientId":"00000000-0000-0000-0000-000000000007","medication":"Atorvastatin 10mg"}', 'corr-rx-634', '2026-07-15 16:00:00+00', '2026-07-15 16:00:05+00', 'Processed')
ON CONFLICT (Id) DO NOTHING;

-- ============================================================================
-- SECTION 6: LAB SERVICE (his_hope_lab)
-- ============================================================================

-- 6a. Lab Orders (5 orders)
INSERT INTO "LabOrders" (Id, PatientId, ProviderId, EncounterId, OrderDate, Status, Priority, Notes, CreatedAt, UpdatedAt) VALUES
-- L001: CBC - Nguyễn Văn A - Đã có kết quả
('00000000-0000-0000-0000-000000000401', '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000102', '00000000-0000-0000-0000-000000000301', '2026-07-15 08:15:00+00', 'Completed', 'Routine', 'Công thức máu toàn phần - kiểm tra sức khỏe tổng quát', '2026-07-15 08:15:00+00', '2026-07-15 11:30:00+00'),
-- L002: Lipid Panel - Đặng Văn G - Đã có kết quả
('00000000-0000-0000-0000-000000000402', '00000000-0000-0000-0000-000000000007', '00000000-0000-0000-0000-000000000103', '00000000-0000-0000-0000-000000000306', '2026-07-15 15:10:00+00', 'Completed', 'Routine', 'Bộ mỡ máu - kiểm tra định kỳ', '2026-07-15 15:10:00+00', '2026-07-15 17:00:00+00'),
-- L003: HbA1c - Vũ Thị F - Đang xử lý
('00000000-0000-0000-0000-000000000403', '00000000-0000-0000-0000-000000000006', '00000000-0000-0000-0000-000000000102', '00000000-0000-0000-0000-000000000303', '2026-07-17 14:05:00+00', 'InProgress', 'Routine', 'HbA1c theo dõi đái tháo đường', '2026-07-17 14:05:00+00', '2026-07-17 14:05:00+00'),
-- L004: Liver Function - Lê Văn C - Đã gửi, chờ lấy mẫu
('00000000-0000-0000-0000-000000000404', '00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000102', '00000000-0000-0000-0000-000000000305', '2026-07-15 10:15:00+00', 'Submitted', 'Routine', 'Chức năng gan - kiểm tra trước khi dùng thuốc', '2026-07-15 10:15:00+00', '2026-07-15 10:15:00+00'),
-- L005: Urinalysis - Nguyễn Văn A - Đã hoàn thành
('00000000-0000-0000-0000-000000000405', '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000102', '00000000-0000-0000-0000-000000000301', '2026-07-15 08:20:00+00', 'Completed', 'Routine', 'Tổng phân tích nước tiểu', '2026-07-15 08:20:00+00', '2026-07-15 10:00:00+00')
ON CONFLICT (Id) DO NOTHING;

-- 6b. Lab Tests
INSERT INTO "LabTests" (Id, LabOrderId, TestCode, TestName, SpecimenType, Status, OrderedAt, CollectedAt, CompletedAt, CreatedAt) VALUES
-- L001 - CBC tests
('00000000-0000-0000-0000-000000000411', '00000000-0000-0000-0000-000000000401', 'CBC-WBC', 'Bạch cầu (WBC)', 'Máu toàn phần', 'Resulted', '2026-07-15 08:15:00+00', '2026-07-15 08:30:00+00', '2026-07-15 11:00:00+00', '2026-07-15 08:15:00+00'),
('00000000-0000-0000-0000-000000000412', '00000000-0000-0000-0000-000000000401', 'CBC-RBC', 'Hồng cầu (RBC)', 'Máu toàn phần', 'Resulted', '2026-07-15 08:15:00+00', '2026-07-15 08:30:00+00', '2026-07-15 11:00:00+00', '2026-07-15 08:15:00+00'),
('00000000-0000-0000-0000-000000000413', '00000000-0000-0000-0000-000000000401', 'CBC-HGB', 'Huyết sắc tố (Hemoglobin)', 'Máu toàn phần', 'Resulted', '2026-07-15 08:15:00+00', '2026-07-15 08:30:00+00', '2026-07-15 11:00:00+00', '2026-07-15 08:15:00+00'),
('00000000-0000-0000-0000-000000000414', '00000000-0000-0000-0000-000000000401', 'CBC-PLT', 'Tiểu cầu (Platelet)', 'Máu toàn phần', 'Resulted', '2026-07-15 08:15:00+00', '2026-07-15 08:30:00+00', '2026-07-15 11:00:00+00', '2026-07-15 08:15:00+00'),
-- L002 - Lipid Panel
('00000000-0000-0000-0000-000000000421', '00000000-0000-0000-0000-000000000402', 'LIP-TC', 'Cholesterol toàn phần', 'Huyết thanh', 'Resulted', '2026-07-15 15:10:00+00', '2026-07-15 15:20:00+00', '2026-07-15 16:45:00+00', '2026-07-15 15:10:00+00'),
('00000000-0000-0000-0000-000000000422', '00000000-0000-0000-0000-000000000402', 'LIP-LDL', 'LDL-Cholesterol', 'Huyết thanh', 'Resulted', '2026-07-15 15:10:00+00', '2026-07-15 15:20:00+00', '2026-07-15 16:45:00+00', '2026-07-15 15:10:00+00'),
('00000000-0000-0000-0000-000000000423', '00000000-0000-0000-0000-000000000402', 'LIP-HDL', 'HDL-Cholesterol', 'Huyết thanh', 'Resulted', '2026-07-15 15:10:00+00', '2026-07-15 15:20:00+00', '2026-07-15 16:45:00+00', '2026-07-15 15:10:00+00'),
('00000000-0000-0000-0000-000000000424', '00000000-0000-0000-0000-000000000402', 'LIP-TG', 'Triglyceride', 'Huyết thanh', 'Resulted', '2026-07-15 15:10:00+00', '2026-07-15 15:20:00+00', '2026-07-15 16:45:00+00', '2026-07-15 15:10:00+00'),
-- L003 - HbA1c (in progress)
('00000000-0000-0000-0000-000000000431', '00000000-0000-0000-0000-000000000403', 'HBA1C', 'HbA1c', 'Máu toàn phần', 'InProgress', '2026-07-17 14:05:00+00', '2026-07-17 14:15:00+00', NULL, '2026-07-17 14:05:00+00'),
-- L004 - Liver Function (chờ lấy mẫu)
('00000000-0000-0000-0000-000000000441', '00000000-0000-0000-0000-000000000404', 'LFT-AST', 'AST (SGOT)', 'Huyết thanh', 'Ordered', '2026-07-15 10:15:00+00', NULL, NULL, '2026-07-15 10:15:00+00'),
('00000000-0000-0000-0000-000000000442', '00000000-0000-0000-0000-000000000404', 'LFT-ALT', 'ALT (SGPT)', 'Huyết thanh', 'Ordered', '2026-07-15 10:15:00+00', NULL, NULL, '2026-07-15 10:15:00+00'),
-- L005 - Urinalysis (completed)
('00000000-0000-0000-0000-000000000451', '00000000-0000-0000-0000-000000000405', 'UAN-PH', 'pH nước tiểu', 'Nước tiểu', 'Resulted', '2026-07-15 08:20:00+00', '2026-07-15 08:35:00+00', '2026-07-15 09:45:00+00', '2026-07-15 08:20:00+00'),
('00000000-0000-0000-0000-000000000452', '00000000-0000-0000-0000-000000000405', 'UAN-GLU', 'Glucose niệu', 'Nước tiểu', 'Resulted', '2026-07-15 08:20:00+00', '2026-07-15 08:35:00+00', '2026-07-15 09:45:00+00', '2026-07-15 08:20:00+00'),
('00000000-0000-0000-0000-000000000453', '00000000-0000-0000-0000-000000000405', 'UAN-PRO', 'Protein niệu', 'Nước tiểu', 'Resulted', '2026-07-15 08:20:00+00', '2026-07-15 08:35:00+00', '2026-07-15 09:45:00+00', '2026-07-15 08:20:00+00')
ON CONFLICT (Id) DO NOTHING;

-- 6c. Lab Results
INSERT INTO "LabResults" (Id, LabTestId, Value, Unit, ReferenceRange, AbnormalFlag, ResultStatus, ResultedAt, PerformedBy, Notes, CreatedAt) VALUES
-- CBC Results (L001) - Normal
('00000000-0000-0000-0000-000000000461', '00000000-0000-0000-0000-000000000411', '7.2', '10^3/uL', '4.0 - 10.0', 'Normal', 'Final', '2026-07-15 11:00:00+00', '00000000-0000-0000-0000-000000000104', NULL, '2026-07-15 11:00:00+00'),
('00000000-0000-0000-0000-000000000462', '00000000-0000-0000-0000-000000000412', '5.1', '10^6/uL', '4.5 - 5.9', 'Normal', 'Final', '2026-07-15 11:00:00+00', '00000000-0000-0000-0000-000000000104', NULL, '2026-07-15 11:00:00+00'),
('00000000-0000-0000-0000-000000000463', '00000000-0000-0000-0000-000000000413', '14.2', 'g/dL', '13.5 - 17.5', 'Normal', 'Final', '2026-07-15 11:00:00+00', '00000000-0000-0000-0000-000000000104', NULL, '2026-07-15 11:00:00+00'),
('00000000-0000-0000-0000-000000000464', '00000000-0000-0000-0000-000000000414', '245', '10^3/uL', '150 - 450', 'Normal', 'Final', '2026-07-15 11:00:00+00', '00000000-0000-0000-0000-000000000104', NULL, '2026-07-15 11:00:00+00'),
-- Lipid Panel Results (L002) - Abnormal (mỡ máu cao)
('00000000-0000-0000-0000-000000000471', '00000000-0000-0000-0000-000000000421', '6.8', 'mmol/L', '< 5.2', 'Abnormal', 'Final', '2026-07-15 16:45:00+00', '00000000-0000-0000-0000-000000000104', 'Cholesterol toàn phần cao', '2026-07-15 16:45:00+00'),
('00000000-0000-0000-0000-000000000472', '00000000-0000-0000-0000-000000000422', '4.2', 'mmol/L', '< 2.6', 'Abnormal', 'Final', '2026-07-15 16:45:00+00', '00000000-0000-0000-0000-000000000104', 'LDL cao, cần điều trị', '2026-07-15 16:45:00+00'),
('00000000-0000-0000-0000-000000000473', '00000000-0000-0000-0000-000000000423', '1.1', 'mmol/L', '> 1.0', 'Normal', 'Final', '2026-07-15 16:45:00+00', '00000000-0000-0000-0000-000000000104', 'HDL trong giới hạn bình thường', '2026-07-15 16:45:00+00'),
('00000000-0000-0000-0000-000000000474', '00000000-0000-0000-0000-000000000424', '3.1', 'mmol/L', '< 1.7', 'Abnormal', 'Final', '2026-07-15 16:45:00+00', '00000000-0000-0000-0000-000000000104', 'Triglyceride tăng cao', '2026-07-15 16:45:00+00'),
-- Urinalysis Results (L005) - Normal
('00000000-0000-0000-0000-000000000481', '00000000-0000-0000-0000-000000000451', '6.0', '', '5.0 - 8.0', 'Normal', 'Final', '2026-07-15 09:45:00+00', '00000000-0000-0000-0000-000000000104', NULL, '2026-07-15 09:45:00+00'),
('00000000-0000-0000-0000-000000000482', '00000000-0000-0000-0000-000000000452', 'Âm tính', '', 'Âm tính', 'Normal', 'Final', '2026-07-15 09:45:00+00', '00000000-0000-0000-0000-000000000104', NULL, '2026-07-15 09:45:00+00'),
('00000000-0000-0000-0000-000000000483', '00000000-0000-0000-0000-000000000453', 'Âm tính', '', 'Âm tính', 'Normal', 'Final', '2026-07-15 09:45:00+00', '00000000-0000-0000-0000-000000000104', NULL, '2026-07-15 09:45:00+00')
ON CONFLICT (Id) DO NOTHING;

-- Lab "OutboxMessages"
INSERT INTO "OutboxMessages" (Id, Type, Content, CorrelationId, OccurredOn, ProcessedOn, Status) VALUES
('00000000-0000-0000-0000-000000000491', 'LabOrderCreated', '{"labOrderId":"00000000-0000-0000-0000-000000000401","patientId":"00000000-0000-0000-0000-000000000001","test":"CBC"}', 'corr-lab-401', '2026-07-15 08:15:00+00', '2026-07-15 08:15:05+00', 'Processed'),
('00000000-0000-0000-0000-000000000492', 'LabOrderCreated', '{"labOrderId":"00000000-0000-0000-0000-000000000402","patientId":"00000000-0000-0000-0000-000000000007","test":"LipidPanel"}', 'corr-lab-402', '2026-07-15 15:10:00+00', '2026-07-15 15:10:05+00', 'Processed'),
('00000000-0000-0000-0000-000000000493', 'LabOrderCreated', '{"labOrderId":"00000000-0000-0000-0000-000000000403","patientId":"00000000-0000-0000-0000-000000000006","test":"HbA1c"}', 'corr-lab-403', '2026-07-17 14:05:00+00', NULL, 'Pending'),
('00000000-0000-0000-0000-000000000494', 'LabOrderCreated', '{"labOrderId":"00000000-0000-0000-0000-000000000404","patientId":"00000000-0000-0000-0000-000000000003","test":"LiverFunction"}', 'corr-lab-404', '2026-07-15 10:15:00+00', '2026-07-15 10:15:05+00', 'Processed'),
('00000000-0000-0000-0000-000000000495', 'LabOrderCreated', '{"labOrderId":"00000000-0000-0000-0000-000000000405","patientId":"00000000-0000-0000-0000-000000000001","test":"Urinalysis"}', 'corr-lab-405', '2026-07-15 08:20:00+00', '2026-07-15 08:20:05+00', 'Processed'),
('00000000-0000-0000-0000-000000000496', 'LabResultReady', '{"labTestId":"00000000-0000-0000-0000-000000000411","patientId":"00000000-0000-0000-0000-000000000001","result":"Normal"}', 'corr-lab-411', '2026-07-15 11:00:00+00', '2026-07-15 11:00:05+00', 'Processed')
ON CONFLICT (Id) DO NOTHING;

-- ============================================================================
-- SECTION 7: BILLING SERVICE (his_hope_billing)
-- ============================================================================

-- 7a. "Invoices" (4 "Invoices")
INSERT INTO "Invoices" (Id, PatientId, EncounterId, InvoiceNumber, InvoiceDate, DueDate, Status, SubTotal, TaxAmount, DiscountAmount, TotalAmount, PaidAmount, BalanceDue, Notes, CreatedAt, UpdatedAt) VALUES
-- I001: Nguyễn Văn A - Đã thanh toán
('00000000-0000-0000-0000-000000000501', '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000301', 'INV-2026-00001', '2026-07-15 08:30:00+00', '2026-07-29 08:30:00+00', 'Paid', 1200000.00, 120000.00, 200000.00, 1120000.00, 1120000.00, 0.00, 'Thanh toán khám tăng huyết áp + xét nghiệm máu', '2026-07-15 08:30:00+00', '2026-07-15 11:30:00+00'),
-- I002: Lê Văn C - Thanh toán một phần
('00000000-0000-0000-0000-000000000502', '00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000305', 'INV-2026-00002', '2026-07-15 10:30:00+00', '2026-07-29 10:30:00+00', 'PartiallyPaid', 850000.00, 85000.00, 0.00, 935000.00, 500000.00, 435000.00, 'Khám viêm dạ dày + nội soi', '2026-07-15 10:30:00+00', '2026-07-15 10:45:00+00'),
-- I003: Đặng Văn G - Hóa đơn nháp
('00000000-0000-0000-0000-000000000503', '00000000-0000-0000-0000-000000000007', '00000000-0000-0000-0000-000000000306', 'INV-2026-00003', '2026-07-15 15:40:00+00', '2026-07-29 15:40:00+00', 'Draft', 650000.00, 65000.00, 0.00, 715000.00, 0.00, 715000.00, 'Khám sức khỏe tổng quát + xét nghiệm mỡ máu', '2026-07-15 15:40:00+00', '2026-07-15 15:40:00+00'),
-- I004: Phạm Thị D - Đã gửi, chờ thanh toán
('00000000-0000-0000-0000-000000000504', '00000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000304', 'INV-2026-00004', '2026-07-16 08:30:00+00', '2026-07-30 08:30:00+00', 'Submitted', 550000.00, 55000.00, 0.00, 605000.00, 0.00, 605000.00, 'Khám hen suyễn cấp + thuốc', '2026-07-16 08:30:00+00', '2026-07-16 08:30:00+00')
ON CONFLICT (Id) DO NOTHING;

-- 7b. Invoice Line Items
INSERT INTO "InvoiceLineItems" (Id, InvoiceId, Description, Quantity, UnitPrice, Amount, ItemCode, ItemType, CreatedAt) VALUES
-- I001 items: Nguyễn Văn A
('00000000-0000-0000-0000-000000000511', '00000000-0000-0000-0000-000000000501', 'Khám bệnh - Tăng huyết áp', 1, 200000.00, 200000.00, 'KB-001', 'Service', '2026-07-15 08:30:00+00'),
('00000000-0000-0000-0000-000000000512', '00000000-0000-0000-0000-000000000501', 'Công thức máu toàn phần (CBC)', 1, 350000.00, 350000.00, 'XN-101', 'Lab', '2026-07-15 08:30:00+00'),
('00000000-0000-0000-0000-000000000513', '00000000-0000-0000-0000-000000000501', 'Tổng phân tích nước tiểu', 1, 250000.00, 250000.00, 'XN-102', 'Lab', '2026-07-15 08:30:00+00'),
('00000000-0000-0000-0000-000000000514', '00000000-0000-0000-0000-000000000501', 'Điện tâm đồ', 1, 400000.00, 400000.00, 'CD-001', 'Procedure', '2026-07-15 08:30:00+00'),
-- I002 items: Lê Văn C
('00000000-0000-0000-0000-000000000521', '00000000-0000-0000-0000-000000000502', 'Khám bệnh - Chuyên khoa Tiêu hóa', 1, 250000.00, 250000.00, 'KB-002', 'Service', '2026-07-15 10:30:00+00'),
('00000000-0000-0000-0000-000000000522', '00000000-0000-0000-0000-000000000502', 'Nội soi dạ dày', 1, 600000.00, 600000.00, 'CD-002', 'Procedure', '2026-07-15 10:30:00+00'),
-- I003 items: Đặng Văn G
('00000000-0000-0000-0000-000000000531', '00000000-0000-0000-0000-000000000503', 'Khám sức khỏe tổng quát', 1, 300000.00, 300000.00, 'KB-003', 'Service', '2026-07-15 15:40:00+00'),
('00000000-0000-0000-0000-000000000532', '00000000-0000-0000-0000-000000000503', 'Bộ mỡ máu (Lipid Panel)', 1, 350000.00, 350000.00, 'XN-201', 'Lab', '2026-07-15 15:40:00+00'),
-- I004 items: Phạm Thị D
('00000000-0000-0000-0000-000000000541', '00000000-0000-0000-0000-000000000504', 'Khám bệnh - Hen suyễn', 1, 200000.00, 200000.00, 'KB-004', 'Service', '2026-07-16 08:30:00+00'),
('00000000-0000-0000-0000-000000000542', '00000000-0000-0000-0000-000000000504', 'Đo chức năng hô hấp', 1, 350000.00, 350000.00, 'CD-003', 'Procedure', '2026-07-16 08:30:00+00')
ON CONFLICT (Id) DO NOTHING;

-- 7c. "Payments"
INSERT INTO "Payments" (Id, InvoiceId, PatientId, Amount, PaymentDate, Method, ReferenceNumber, Status, Notes, CreatedAt) VALUES
('00000000-0000-0000-0000-000000000551', '00000000-0000-0000-0000-000000000501', '00000000-0000-0000-0000-000000000001', 1120000.00, '2026-07-15 11:30:00+00', 'Cash', 'CS-001-20260715', 'Completed', 'Thanh toán toàn bộ bằng tiền mặt', '2026-07-15 11:30:00+00'),
('00000000-0000-0000-0000-000000000552', '00000000-0000-0000-0000-000000000502', '00000000-0000-0000-0000-000000000003', 500000.00, '2026-07-15 10:45:00+00', 'Insurance', 'BH-002-20260715', 'Completed', 'Thanh toán tạm ứng qua bảo hiểm', '2026-07-15 10:45:00+00')
ON CONFLICT (Id) DO NOTHING;

-- Billing "OutboxMessages"
INSERT INTO "OutboxMessages" (Id, Type, Content, CorrelationId, OccurredOn, ProcessedOn, Status) VALUES
('00000000-0000-0000-0000-000000000561', 'InvoiceCreated', '{"invoiceId":"00000000-0000-0000-0000-000000000501","patientId":"00000000-0000-0000-0000-000000000001","amount":1120000.00}', 'corr-inv-501', '2026-07-15 08:30:00+00', '2026-07-15 08:30:05+00', 'Processed'),
('00000000-0000-0000-0000-000000000562', 'PaymentReceived', '{"invoiceId":"00000000-0000-0000-0000-000000000501","amount":1120000.00,"method":"Cash"}', 'corr-pay-551', '2026-07-15 11:30:00+00', '2026-07-15 11:30:05+00', 'Processed'),
('00000000-0000-0000-0000-000000000563', 'InvoiceCreated', '{"invoiceId":"00000000-0000-0000-0000-000000000502","patientId":"00000000-0000-0000-0000-000000000003","amount":935000.00}', 'corr-inv-502', '2026-07-15 10:30:00+00', '2026-07-15 10:30:05+00', 'Processed'),
('00000000-0000-0000-0000-000000000564', 'PaymentReceived', '{"invoiceId":"00000000-0000-0000-0000-000000000502","amount":500000.00,"method":"Insurance"}', 'corr-pay-552', '2026-07-15 10:45:00+00', '2026-07-15 10:45:05+00', 'Processed'),
('00000000-0000-0000-0000-000000000565', 'InvoiceCreated', '{"invoiceId":"00000000-0000-0000-0000-000000000503","patientId":"00000000-0000-0000-0000-000000000007","amount":715000.00}', 'corr-inv-503', '2026-07-15 15:40:00+00', NULL, 'Pending'),
('00000000-0000-0000-0000-000000000566', 'InvoiceCreated', '{"invoiceId":"00000000-0000-0000-0000-000000000504","patientId":"00000000-0000-0000-0000-000000000004","amount":605000.00}', 'corr-inv-504', '2026-07-16 08:30:00+00', NULL, 'Pending')
ON CONFLICT (Id) DO NOTHING;