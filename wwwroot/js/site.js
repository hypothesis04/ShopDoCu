// --- PHẦN 1: SCROLL MƯỢT & XỬ LÝ LINK ---
function handleSmoothScroll(event, targetId) {
    const targetUrl = event.currentTarget.getAttribute('href');
    const currentPath = window.location.pathname;

    if (targetUrl !== '#' && !currentPath.endsWith(targetUrl)) {
        localStorage.setItem('scrollToTarget', targetId);
        return true;
    }
    event.preventDefault();
    const targetElement = document.getElementById(targetId);
    if (targetElement) {
        targetElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
        targetElement.querySelector('input[type="text"], input[type="email"], input[type="password"]')?.focus();
    }
}

document.addEventListener('DOMContentLoaded', function() {
    const targetId = localStorage.getItem('scrollToTarget');
    if (targetId) {
        setTimeout(() => {
            const targetElement = document.getElementById(targetId);
            if (targetElement) {
                targetElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
                targetElement.querySelector('input[type="text"], input[type="email"]')?.focus();
            }
            localStorage.removeItem('scrollToTarget');
        }, 100);
    }
});

// --- PHẦN 2: ĐĂNG KÝ BÁN HÀNG (AJAX) ---
$(function () {
    var sellerForm = $('#seller-registration-form');
    if (sellerForm.length) {
        sellerForm.on('submit', function (e) {
            e.preventDefault();
            var form = $(this);
            
            if (!form.valid()) {
                var firstError = form.find('.is-invalid').first();
                 if (firstError.length) {
                    firstError[0].scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
                return;
            }

            $.ajax({
                type: "POST",
                url: form.attr('action'),
                data: form.serialize(),
                success: function (response) {
                    if (response.success) {
                        var successHtml = `
                            <div class="text-center my-5">
                                <i class="bi bi-check-circle-fill text-success" style="font-size: 4rem;"></i>
                                <h4 class="mt-3 text-success">${response.message}</h4>
                                <p class="lead">Yêu cầu đang chờ duyệt.</p>
                                <a href="/" class="btn btn-primary mt-3">Về Trang chủ</a>
                            </div>`;
                        $('#registration-container').html(successHtml);
                    } else {
                        var errorDiv = $('#form-errors');
                        var errorList = '<ul>' + (response.errors ? response.errors.map(e => `<li>${e}</li>`).join('') : `<li>${response.message}</li>`) + '</ul>';
                        errorDiv.html(errorList).show();
                        errorDiv[0].scrollIntoView({ behavior: 'smooth', block: 'center' });
                    }
                },
                error: function () {
                    var errorDiv = $('#form-errors');
                    errorDiv.html('Lỗi kết nối server. Vui lòng thử lại.').show();
                    errorDiv[0].scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
            });
        });
    }
});

// --- PHẦN 3: TẠO SẢN PHẨM (Create Product) ---
$(function () {
    const parentCategory = $('#parentCategory');
    const childCategory = $('#childCategory');
    const imageInput = $('#productImages'); // ID chuẩn bên View
    const imagePreview = $('#imagePreview');
    const mainImageSelect = $('#mainImageSelect');

    // Chỉ chạy nếu đang ở trang có form này
    if (imageInput.length === 0) return;

    // 1. Load danh mục con
    parentCategory.on('change', function () {
        const parentId = $(this).val();
        // Lấy URL từ data-url hoặc fallback về cứng (để an toàn)
        const url = $(this).data('url') || '/Product/GetChildCategories'; 
        
        childCategory.prop('disabled', true).html('<option value="">-- Đang tải... --</option>');

        if (parentId) {
            $.getJSON(url, { parentId: parentId }, function(data) {
                if (data && data.length > 0) {
                    childCategory.empty().append('<option value="">-- Chọn danh mục con --</option>');
                    $.each(data, function(i, item) {
                        childCategory.append($('<option>').val(item.categoryId).text(item.categoryName));
                    });
                    childCategory.prop('disabled', false);
                } else {
                    childCategory.html('<option value="">-- Không có danh mục con --</option>');
                }
            }).fail(function() {
                childCategory.html('<option value="">-- Lỗi tải dữ liệu --</option>');
            });
        } else {
            childCategory.html('<option value="">-- Chọn danh mục cha trước --</option>');
        }
    });

    // 2. Preview ảnh & Main Image
    imageInput.on('change', function (e) {
        const files = e.target.files;
        
        // RESET lại giao diện
        imagePreview.empty(); 
        mainImageSelect.empty().append('<option value="">-- Chọn ảnh chính --</option>');
        mainImageSelect.prop('disabled', true);

        if (files.length === 0) return;

        // Cảnh báo nếu ít hơn 3 ảnh
        if (files.length < 3) {
            imagePreview.html('<div class="col-12"><div class="alert alert-warning py-2"><i class="bi bi-exclamation-triangle me-2"></i>Vui lòng chọn tối thiểu 3 ảnh.</div></div>');
        }

        // Mở khóa dropdown
        mainImageSelect.prop('disabled', false);

        // Duyệt file
        $.each(files, function(index, file) {
            const reader = new FileReader();
            reader.onload = function (event) {
                const html = `
                    <div class="col-6 col-md-3 mb-3">
                        <div class="card h-100 border-0 shadow-sm">
                            <img src="${event.target.result}" class="card-img-top" style="height: 120px; object-fit: contain;">
                            <div class="card-footer p-1 text-center small bg-white">
                                <strong>Ảnh ${index + 1}</strong>
                            </div>
                        </div>
                    </div>`;
                imagePreview.append(html);
            };
            reader.readAsDataURL(file);

            // Thêm vào dropdown
            mainImageSelect.append(`<option value="${index}">Ảnh ${index + 1}</option>`);
        });

        // TỰ ĐỘNG CHỌN ẢNH ĐẦU TIÊN (UX tốt hơn)
        mainImageSelect.val(0);
    });
});