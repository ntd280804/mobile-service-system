-- Stored Procedure để hủy đơn hàng
-- Logic:
-- 1. Chuyển STATUS của PART về "Trong kho"
-- 2. Set ORDER_ID trong PART = NULL
-- 3. Chuyển STATUS của ORDER thành "Đã hủy"

CREATE OR REPLACE PROCEDURE CANCEL_ORDER(
    p_order_id IN NUMBER,
    p_result OUT VARCHAR2
)
AS
    v_order_exists NUMBER;
    v_parts_count NUMBER;
BEGIN
    -- Kiểm tra ORDER có tồn tại không
    SELECT COUNT(*) INTO v_order_exists
    FROM ORDERS
    WHERE ORDER_ID = p_order_id;
    
    IF v_order_exists = 0 THEN
        p_result := 'Đơn hàng không tồn tại.';
        RETURN;
    END IF;
    
    -- Kiểm tra ORDER đã bị hủy chưa
    SELECT COUNT(*) INTO v_order_exists
    FROM ORDERS
    WHERE ORDER_ID = p_order_id AND STATUS = 'Đã hủy';
    
    IF v_order_exists > 0 THEN
        p_result := 'Đơn hàng đã được hủy trước đó.';
        RETURN;
    END IF;
    
    -- Đếm số PART liên quan
    SELECT COUNT(*) INTO v_parts_count
    FROM PART
    WHERE ORDER_ID = p_order_id;
    
    -- 1. Cập nhật PART: chuyển STATUS về "Trong kho" và set ORDER_ID = NULL
    UPDATE PART
    SET STATUS = 'Trong kho',
        ORDER_ID = NULL
    WHERE ORDER_ID = p_order_id;
    
    -- 2. Cập nhật STATUS của ORDER thành "Đã hủy"
    UPDATE ORDERS
    SET STATUS = 'Đã hủy'
    WHERE ORDER_ID = p_order_id;
    
    -- Trả về kết quả
    IF v_parts_count > 0 THEN
        p_result := 'Hủy đơn hàng thành công. Đã giải phóng ' || v_parts_count || ' linh kiện về kho.';
    ELSE
        p_result := 'Hủy đơn hàng thành công.';
    END IF;
    
    COMMIT;
    
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_result := 'Lỗi khi hủy đơn hàng: ' || SQLERRM;
        RAISE;
END CANCEL_ORDER;
/

-- Grant quyền thực thi
GRANT EXECUTE ON APP.CANCEL_ORDER TO ROLE_ADMIN;
GRANT EXECUTE ON APP.CANCEL_ORDER TO ROLE_TIEPTAN;

