#include <QApplication>
#include <QPushButton>
#include "QtUI.h"

int main(int argc, char *argv[]) {
    QApplication a(argc, argv);
    DragDropWidget widget;
    widget.setFrameStyle(QFrame::Sunken | QFrame::StyledPanel);
    widget.setGeometry(100, 100, 300, 200);
    widget.show();
    return QApplication::exec();
}
